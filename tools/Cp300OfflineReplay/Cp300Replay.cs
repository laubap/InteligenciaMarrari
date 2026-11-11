using System.Globalization;
using System.Text;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace IAApi.Cp300OfflineReplay;

// Reproducao offline da deteccao univariada sobre o historico exportado do
// Bridge (CSV). Isolamento: SQLite em memoria, historico servido do CSV, sem
// Bridge, sem banco real, sem Isolation Forest, sem multivariado. Nao altera
// nenhum codigo de runtime: apenas instancia os servicos de producao.

/// <summary>Parametros de uma execucao.</summary>
public sealed record Cp300ReplayOptions
{
    /// <summary>CSV de entrada: ClienteId,TagName,DataHora,Valor.</summary>
    public required string CsvEntrada { get; init; }

    /// <summary>CSV de saida (uma linha por leitura e detector).</summary>
    public required string CsvSaida { get; init; }

    /// <summary>
    /// Instante T do treino "como em producao": o perfil e treinado com as
    /// leituras de [T - HorasHistorico, T). O replay comeca em T.
    /// </summary>
    public required DateTime InstanteTreino { get; init; }

    /// <summary>appsettings.json de onde saem pesos, limiares e HorasHistorico.</summary>
    public required string AppSettingsPath { get; init; }

    /// <summary>Fim do replay (inclusivo). Null = ate a ultima leitura do CSV.</summary>
    public DateTime? FimReplay { get; init; }

    /// <summary>Cliente usado quando o CSV nao tem a coluna ClienteId.</summary>
    public string? ClienteId { get; init; }

    /// <summary>Tags a reproduzir. Null = todas do CSV, exceto rotulos TAG60-TAG81.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Substitui Bridge:HorasHistorico do appsettings (uso em testes).</summary>
    public int? HorasHistorico { get; init; }

    /// <summary>
    /// Se true, o historico servido a leitura t inclui o proprio ponto t.
    /// Padrao false: nao se sabe se o PSi4 ja devolve o minuto corrente.
    /// </summary>
    public bool HistoricoIncluiLeituraAtual { get; init; }

    /// <summary>Destino das mensagens da ferramenta (nao do runtime).</summary>
    public TextWriter? Log { get; init; }
}

public sealed record Cp300ReplaySummary(
    int TagsReproduzidas,
    int Leituras,
    int AnomaliasPredictionService,
    int LeiturasComErro,
    int Divergencias,
    IReadOnlyList<string> TagsIgnoradas,
    int HorasHistorico,
    int ValoresDescartados);

public static class Cp300Replay
{
    public const string TipoTagPadrao = "perfil-comportamental";

    public static readonly string[] Colunas =
    [
        "data_hora", "cliente", "tag", "valor",
        "detector", "aplicavel", "participou", "motivo_nao_aplicavel",
        "nota_detector", "peso", "contribuicao",
        "nota_final", "limiar", "decisao_ensemble", "decisao_final",
        "tipo_tag", "confianca_tipo", "roteamento_por_tipo",
        "deadband", "origem_deadband", "confianca_deadband", "origem_baseline",
        "media", "desvio", "pontos_historico",
        "entrada_invalida", "motivo_entrada_invalida", "erro", "conferencia"
    ];

    public static async Task<Cp300ReplaySummary> RunAsync(Cp300ReplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var log = options.Log ?? TextWriter.Null;

        var (detectionOptions, horasHistorico) = LoadSettings(options);
        var dataset = Cp300Csv.Load(options.CsvEntrada, options.ClienteId);
        var tags = SelectTags(dataset, options.Tags);
        if (tags.Count == 0)
            throw new InvalidOperationException("Nenhuma tag para reproduzir.");

        var clienteId = dataset.ClienteId;
        var t0 = options.InstanteTreino;
        var fim = options.FimReplay ?? dataset.Readings.Values.Max(r => r[^1].DataHora);
        if (fim < t0)
            throw new InvalidOperationException("O fim do replay e anterior ao instante de treino.");

        log.WriteLine($"Cliente: {clienteId} | Tags: {tags.Count} | Janela: {horasHistorico} h");
        log.WriteLine($"Treino: [{t0.AddHours(-horasHistorico):O}, {t0:O}) | Replay: [{t0:O}, {fim:O}]");
        log.WriteLine($"Pesos (appsettings): Z={detectionOptions.ZScoreWeight} MAD={detectionOptions.MadWeight} " +
                      $"EWMA={detectionOptions.EwmaWeight} RoC={detectionOptions.RateOfChangeWeight} " +
                      $"Persist={detectionOptions.PersistenceWeight} LevelShift={detectionOptions.LevelShiftWeight} " +
                      $"Limiar={detectionOptions.EnsembleThreshold} | Isolation Forest: desligado");

        var history = new CsvHistorySource(dataset.Readings, horasHistorico, options.HistoricoIncluiLeituraAtual);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        // Sem TagContextoIa: o tipo fica no padrao de producao
        // ("perfil-comportamental") e a classificacao real decide o roteamento.
        foreach (var tag in tags)
            db.TagsIaConfig.Add(new TagIaConfig { ClienteId = clienteId, TagName = tag, IaAtiva = true });
        await db.SaveChangesAsync();

        // Treino "como em producao": PerfilTagService real, historico = ultimas
        // HorasHistorico horas antes de T.
        var ignoradas = new List<string>();
        var treinadas = new List<string>();
        history.Clock = t0;
        var trainer = new PerfilTagService(history, db, new HistoricoLimpezaService());
        foreach (var tag in tags)
        {
            try
            {
                var perfil = trainer.TreinarPerfil(clienteId, tag);
                perfil.DataTreinamento = t0;
                await db.SaveChangesAsync();
                treinadas.Add(tag);
                log.WriteLine($"Perfil {tag}: media={Fmt(perfil.Media)} desvio={Fmt(perfil.DesvioPadrao)} " +
                              $"deadband={Fmt(perfil.VariacaoMinimaRelevante)} usados={perfil.TotalRegistrosUsados} " +
                              $"outliers={perfil.TotalOutliersRemovidos}");
            }
            catch (Exception ex)
            {
                ignoradas.Add($"{tag}: treino falhou ({ex.Message})");
                log.WriteLine($"Perfil {tag}: NAO treinado ({ex.Message})");
            }
        }

        var production = BuildPredictionService(db, history, detectionOptions);
        // Detalhamento: mesmas classes de producao, instancias separadas, sobre
        // o mesmo DetectorInput. A decisao oficial continua vindo do PredictionService.
        var detailPreparation = new PredictionDetectionPreparationService(history);
        var detailEnsemble = BuildEnsemble(detectionOptions);
        var resolver = new TagBehaviorProfileResolver();

        var readings = treinadas
            .SelectMany(tag => dataset.Readings[tag]
                .Where(r => r.DataHora >= t0 && r.DataHora <= fim)
                .Select(r => (Tag: tag, Reading: r)))
            .OrderBy(x => x.Reading.DataHora)
            .ThenBy(x => x.Tag, StringComparer.Ordinal)
            .ToList();

        var leituras = 0;
        var anomalias = 0;
        var erros = 0;
        var divergencias = 0;

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.CsvSaida));
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);
        await using var writer = new StreamWriter(options.CsvSaida, false, new UTF8Encoding(true));
        await writer.WriteLineAsync(string.Join(',', Colunas));

        foreach (var (tag, reading) in readings)
        {
            leituras++;
            history.Clock = reading.DataHora;
            var request = new AnomaliaRequest
            {
                ClienteId = clienteId,
                TagName = tag,
                Valor = reading.Valor,
                DataHora = reading.DataHora
            };

            var row = new ReadingRow(reading.DataHora, clienteId, tag, reading.Valor);

            AnomaliaResponse? response = null;
            try
            {
                response = await production.Detectar(request);
                row.DecisaoFinal = response.EhAnomalia;
                if (response.EhAnomalia)
                    anomalias++;
            }
            catch (Exception ex)
            {
                row.Erro = "PredictionService: " + ex.Message;
            }

            try
            {
                var perfil = await db.PerfisIa.AsNoTracking()
                    .SingleAsync(p => p.ClienteId == clienteId && p.TagName == tag);
                var prepared = await detailPreparation.PrepararAsync(
                    request, perfil, TipoTagPadrao, reading.DataHora);
                FillDetail(row, prepared, detailEnsemble, resolver);
            }
            catch (Exception ex)
            {
                row.Erro = row.Erro is null
                    ? "Detalhamento: " + ex.Message
                    : row.Erro + " | Detalhamento: " + ex.Message;
            }

            row.Conferencia = Check(row, response);
            if (row.Erro is not null)
                erros++;
            if (row.Conferencia.StartsWith("divergente", StringComparison.Ordinal))
                divergencias++;

            foreach (var line in row.ToCsvLines())
                await writer.WriteLineAsync(line);
        }

        var summary = new Cp300ReplaySummary(treinadas.Count, leituras, anomalias, erros, divergencias,
            ignoradas, horasHistorico, dataset.DiscardedValues);
        log.WriteLine($"Leituras: {leituras} | Anomalias: {anomalias} | Erros: {erros} | " +
                      $"Divergencias: {divergencias} | Valores descartados no CSV: {dataset.DiscardedValues}");
        return summary;
    }

    private static (AnomalyDetectionOptions Options, int Horas) LoadSettings(Cp300ReplayOptions options)
    {
        var path = Path.GetFullPath(options.AppSettingsPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("appsettings nao encontrado.", path);

        // Somente as secoes de deteccao e a janela de historico sao lidas.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .Build();

        var detection = new AnomalyDetectionOptions();
        configuration.GetSection("AnomalyDetection").Bind(detection);
        var validation = new AnomalyDetectionOptionsValidator().Validate(null, detection);
        if (validation.Failed)
            throw new InvalidOperationException("AnomalyDetection invalido: " + validation.FailureMessage);

        // Mesma regra de BridgeHistoricoService.BuscarHistorico.
        var horas = options.HorasHistorico;
        if (horas is null)
        {
            var texto = configuration["Bridge:HorasHistorico"] ?? "24";
            horas = int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h > 0
                ? h
                : 24;
        }
        else if (horas <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.HorasHistorico));
        }

        return (detection, horas.Value);
    }

    private static List<string> SelectTags(Cp300Dataset dataset, IReadOnlyList<string>? requested)
    {
        if (requested is null || requested.Count == 0)
        {
            return dataset.Readings.Keys
                .Where(tag => !IsLabelTag(tag))
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList();
        }

        var result = new List<string>();
        foreach (var tag in requested)
        {
            if (IsLabelTag(tag))
                throw new InvalidOperationException(
                    $"{tag} e rotulo do simulador (TAG60-TAG81) e nunca pode ser entrada da IA.");
            if (!dataset.Readings.ContainsKey(tag))
                throw new InvalidOperationException($"Tag {tag} nao existe no CSV.");
            result.Add(tag);
        }

        return result;
    }

    /// <summary>TAG60-TAG81 sao gabarito do simulador CP300.</summary>
    public static bool IsLabelTag(string tag)
    {
        var name = tag.Contains('.') ? tag[(tag.LastIndexOf('.') + 1)..] : tag;
        return name.StartsWith("TAG", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(name.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var n) &&
               n is >= 60 and <= 81;
    }

    private static IAnomalyDetector[] BuildDetectors(AnomalyDetectionOptions options) =>
    [
        // Mesma ordem de LegacyAnomalyDetectionServiceCollectionExtensions.
        new StatisticalDetector(options),
        new MadDetector(options),
        new EwmaDetector(options),
        new RateOfChangeDetector(options),
        new PersistenceDetector(options),
        new LevelShiftDetector(options)
    ];

    private static EnsembleDetectionService BuildEnsemble(AnomalyDetectionOptions options) =>
        new(BuildDetectors(options), Options.Create(options),
            new TagBehaviorProfileResolver(), new DetectorApplicabilityPolicy());

    private static PredictionService BuildPredictionService(
        AppDbContext db, CsvHistorySource history, AnomalyDetectionOptions options)
    {
        var ensemble = BuildEnsemble(options);
        var interpretation = new PredictionInterpretationService(
            new AnomalyInterpretationContextBuilder(new TagContextoIaService(db)),
            new OfflineInterpretation());
        var runtimeContext = new RuntimeFunctionalDecisionContextProvider(
            new RuntimeFunctionalDecisionStateStore(), Options.Create(options));
        // Runtime desligado = decisao legada do ensemble (caso do PK2, fora do canario).
        var runtimeDecision = new PredictionRuntimeDecisionService(
            new RuntimeFunctionalDecisionCandidateFactory(),
            new RuntimeAnomalyDecisionResolver(),
            new RuntimeAnomalyDecisionOptions { Enabled = false },
            new RuntimeDecisionMetrics());
        var decision = new PredictionDecisionOrchestrationService(
            ensemble, interpretation, runtimeContext, runtimeDecision);
        // BridgeValorAtualService nulo: sem TagDependenciaIa no banco em memoria,
        // nada e consultado; se fosse, falharia em vez de acessar o Bridge.
        var postDecision = new PredictionPostDecisionAnalysisService(
            new AnaliseRelacionadasService(db, null!),
            new PredictionRiskAnalysisService(new RiskScoreService(), new ExplicacaoRiscoService()),
            new PredictionTrendAnalysisService(new TendenciaRiscoService(db), new TendenciaValorService(history)),
            new FeedbackAnomaliaAnaliseService(db));
        var finalization = new PredictionFinalizationService(
            new AnomaliaHistoricoService(db),
            new PredictionResponseBuilder(new PredictionDependencyMapper(new InterpretacaoDependenciasService())));

        return new PredictionService(
            new PredictionContextLoader(db, new ProcessoAnaliseService(db)),
            postDecision, finalization, decision,
            new PredictionDetectionPreparationService(history),
            multivariateShadowRuntimeCoordinator: null);
    }

    private static void FillDetail(
        ReadingRow row,
        PredictionDetectionPreparationResult prepared,
        EnsembleDetectionService ensemble,
        TagBehaviorProfileResolver resolver)
    {
        var input = prepared.EntradaDetector;
        row.Media = input.MediaHistorica;
        row.Desvio = input.DesvioPadraoHistorico;
        row.PontosHistorico = input.Historico.Count;
        row.OrigemBaseline = prepared.BaselineEfetivo?.Kind.ToString() ?? "desconhecida";

        var protection = BaselineProtectionProfile.Build(input, input.MediaHistorica);
        row.Deadband = protection.Deadband;
        row.OrigemDeadband = protection.DeadbandSource;
        row.ConfiancaDeadband = protection.DeadbandConfidence;

        var validation = DetectionInputValidator.Validate(input);
        row.EntradaInvalida = !validation.IsValid;
        if (!validation.IsValid)
            row.MotivoEntradaInvalida = string.Join(" | ", validation.Errors);
        else
        {
            var profile = input.ObterPerfilComportamental(resolver);
            row.TipoTag = profile.Type.ToString();
            row.ConfiancaTipo = profile.Confidence;
            row.RoteamentoPorTipo = profile.IsHighConfidence(DetectorApplicabilityPolicy.MinimumRoutingConfidence);
        }

        var result = ensemble.Detectar(input);
        row.NotaFinal = result.ScoreNormalizado;
        row.ScoreEnsemble = result.Score;
        row.DecisaoEnsemble = result.EhAnomalia;
        if (result.Metricas is not null)
        {
            if (result.Metricas.TryGetValue("limiteEnsemble", out var limiar))
                row.Limiar = limiar;
            if (result.Metricas.TryGetValue("somaPesosAplicaveis", out var soma))
                row.SomaPesos = soma;
        }

        foreach (var detail in result.DetalhesDetectores ?? [])
        {
            var participou = detail.ParticipatedInAggregation == true;
            row.Detectores.Add(new DetectorRow(
                detail.Nome,
                detail.Aplicavel,
                participou,
                detail.Aplicavel ? null : detail.NonApplicabilityReasonCode ?? detail.ExecutionReasonCode,
                detail.ScoreNormalizado,
                detail.ConfiguredWeight ?? detail.Peso,
                participou && row.SomaPesos > 0
                    ? detail.ScoreNormalizado * detail.Peso / row.SomaPesos.Value
                    : 0));
        }
    }

    private static string Check(ReadingRow row, AnomaliaResponse? response)
    {
        if (response is null || row.DecisaoEnsemble is null)
            return "nao_conferido";

        var problemas = new List<string>();
        if (response.EhAnomalia != row.DecisaoEnsemble)
            problemas.Add("decisao");
        if (row.ScoreEnsemble is double score && Math.Abs(response.Score - score) > 1e-4)
            problemas.Add("nota");
        if (row.NotaFinal is double final && row.Detectores.Count > 0 && row.SomaPesos > 0)
        {
            var soma = row.Detectores.Sum(d => d.Contribuicao);
            if (Math.Abs(soma - final) > 1e-6)
                problemas.Add("soma_contribuicoes");
        }

        return problemas.Count == 0 ? "ok" : "divergente:" + string.Join('+', problemas);
    }

    internal static string Fmt(double? value) =>
        value is double v ? v.ToString("G9", CultureInfo.InvariantCulture) : "";

    private sealed class OfflineInterpretation : IAnomalyInterpretationService
    {
        public Task<AnomalyInterpretationResult> InterpretarAsync(
            AnomalyInterpretationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AnomalyInterpretationResult
            {
                Resumo = "Replay offline CP300",
                InterpretacaoTecnica = "Replay offline CP300",
                Severidade = "Desconhecida",
                ModeloUtilizado = "Replay"
            });
    }

    private sealed record DetectorRow(
        string Nome, bool Aplicavel, bool Participou, string? MotivoNaoAplicavel,
        double Nota, double Peso, double Contribuicao);

    private sealed class ReadingRow(DateTime dataHora, string cliente, string tag, float valor)
    {
        public List<DetectorRow> Detectores { get; } = [];
        public double? NotaFinal { get; set; }
        public double? ScoreEnsemble { get; set; }
        public double? Limiar { get; set; }
        public double? SomaPesos { get; set; }
        public bool? DecisaoEnsemble { get; set; }
        public bool? DecisaoFinal { get; set; }
        public string? TipoTag { get; set; }
        public double? ConfiancaTipo { get; set; }
        public bool? RoteamentoPorTipo { get; set; }
        public double? Deadband { get; set; }
        public string? OrigemDeadband { get; set; }
        public double? ConfiancaDeadband { get; set; }
        public string? OrigemBaseline { get; set; }
        public double? Media { get; set; }
        public double? Desvio { get; set; }
        public int? PontosHistorico { get; set; }
        public bool? EntradaInvalida { get; set; }
        public string? MotivoEntradaInvalida { get; set; }
        public string? Erro { get; set; }
        public string Conferencia { get; set; } = "nao_conferido";

        public IEnumerable<string> ToCsvLines()
        {
            if (Detectores.Count == 0)
            {
                yield return Line(null);
                yield break;
            }

            foreach (var detector in Detectores)
                yield return Line(detector);
        }

        private string Line(DetectorRow? d)
        {
            string[] fields =
            [
                dataHora.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                cliente, tag, Fmt(valor),
                d?.Nome ?? "", Bool(d?.Aplicavel), Bool(d?.Participou), d?.MotivoNaoAplicavel ?? "",
                Fmt(d?.Nota), Fmt(d?.Peso), Fmt(d?.Contribuicao),
                Fmt(NotaFinal), Fmt(Limiar), Bool(DecisaoEnsemble), Bool(DecisaoFinal),
                TipoTag ?? "", Fmt(ConfiancaTipo), Bool(RoteamentoPorTipo),
                Fmt(Deadband), OrigemDeadband ?? "", Fmt(ConfiancaDeadband), OrigemBaseline ?? "",
                Fmt(Media), Fmt(Desvio), PontosHistorico?.ToString(CultureInfo.InvariantCulture) ?? "",
                Bool(EntradaInvalida), MotivoEntradaInvalida ?? "", Erro ?? "", Conferencia
            ];
            return string.Join(',', fields.Select(Cp300Csv.Escape));
        }

        private static string Bool(bool? value) => value is null ? "" : value.Value ? "1" : "0";
    }
}

internal sealed record Cp300Dataset(
    string ClienteId,
    IReadOnlyDictionary<string, LeituraBruta[]> Readings,
    int DiscardedValues);

/// <summary>
/// Leitura do CSV gerado por Export-Cp300Historico.ps1.
/// Cabecalho: ClienteId,TagName,DataHora,Valor (ClienteId opcional).
/// DataHora sem fuso, como o Bridge entrega. Valor com ponto decimal.
/// Valores nao numericos/NaN/Infinity sao descartados e duplicados por
/// horario ficam com o ultimo, como em BridgeHistoricoService.ConverterHistorico.
/// </summary>
internal static class Cp300Csv
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF"
    ];

    public static Cp300Dataset Load(string path, string? clienteOverride)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = reader.ReadLine() ?? throw new InvalidDataException("CSV vazio.");
        var columns = Split(header).Select(c => c.Trim()).ToArray();
        int Index(string name) => Array.FindIndex(columns, c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
        var iCliente = Index("ClienteId");
        var iTag = Index("TagName");
        var iData = Index("DataHora");
        var iValor = Index("Valor");
        if (iTag < 0 || iData < 0 || iValor < 0)
            throw new InvalidDataException("Cabecalho esperado: ClienteId,TagName,DataHora,Valor.");

        var clientes = new HashSet<string>(StringComparer.Ordinal);
        var series = new Dictionary<string, SortedDictionary<DateTime, float>>(StringComparer.Ordinal);
        var discarded = 0;
        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var fields = Split(line);
            if (fields.Count <= Math.Max(iTag, Math.Max(iData, iValor)))
                throw new InvalidDataException($"Linha {lineNumber}: colunas insuficientes.");

            if (iCliente >= 0 && !string.IsNullOrWhiteSpace(fields[iCliente]))
                clientes.Add(fields[iCliente].Trim());

            var tag = fields[iTag].Trim();
            if (!DateTime.TryParseExact(fields[iData].Trim(), DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dataHora))
                throw new InvalidDataException(
                    $"Linha {lineNumber}: DataHora '{fields[iData]}' invalida (use yyyy-MM-ddTHH:mm:ss, sem fuso).");

            if (!float.TryParse(fields[iValor].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var valor) || float.IsNaN(valor) || float.IsInfinity(valor))
            {
                discarded++;
                continue;
            }

            if (!series.TryGetValue(tag, out var points))
                series[tag] = points = new SortedDictionary<DateTime, float>();
            points[dataHora] = valor;
        }

        string cliente;
        if (!string.IsNullOrWhiteSpace(clienteOverride))
            cliente = clienteOverride.Trim();
        else if (clientes.Count == 1)
            cliente = clientes.First();
        else if (clientes.Count == 0)
            throw new InvalidDataException("CSV sem ClienteId: informe --cliente.");
        else
            throw new InvalidDataException("CSV com mais de um ClienteId: informe --cliente.");

        var readings = series.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(p => new LeituraBruta { DataHora = p.Key, Valor = p.Value }).ToArray(),
            StringComparer.Ordinal);

        return new Cp300Dataset(cliente, readings, discarded);
    }

    public static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    private static List<string> Split(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }
}

/// <summary>
/// IHistoricoService offline. BuscarHistorico devolve a janela de
/// HorasHistorico horas que termina no relogio atual (a leitura em replay),
/// imitando GET /historico?horas=N do Bridge.
/// </summary>
internal sealed class CsvHistorySource(
    IReadOnlyDictionary<string, LeituraBruta[]> readings,
    int horas,
    bool incluiLeituraAtual) : IHistoricoService
{
    public DateTime Clock { get; set; }

    public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag)
    {
        var inicio = Clock.AddHours(-horas);
        return Slice(tagName, inicio, Clock, incluiLeituraAtual);
    }

    public List<LeituraBruta> BuscarHistoricoPorPeriodo(
        string clienteId, string tagName, DateTime inicio, DateTime fim)
        => Slice(tagName, inicio, fim, inclusiveEnd: true);

    private List<LeituraBruta> Slice(string tag, DateTime inicio, DateTime fim, bool inclusiveEnd)
    {
        if (!readings.TryGetValue(tag, out var series))
            return [];

        var start = LowerBound(series, inicio);
        var result = new List<LeituraBruta>();
        for (var i = start; i < series.Length; i++)
        {
            var t = series[i].DataHora;
            if (t > fim || (!inclusiveEnd && t == fim))
                break;
            // Copia: o runtime nao deve conseguir alterar a serie de origem.
            result.Add(new LeituraBruta { DataHora = t, Valor = series[i].Valor });
        }

        return result;
    }

    private static int LowerBound(LeituraBruta[] series, DateTime value)
    {
        int lo = 0, hi = series.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (series[mid].DataHora < value) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }
}
