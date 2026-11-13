using IAApi.Infrastructure.Observability;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.DependencyInjection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// P31 (BACKLOG 25): recorte causal do histórico na preparação da detecção.
///
/// Reproduz o caso de 12/11 com dados sintéticos: o monitor lê o valor da tag
/// em lote no início do ciclo (minuto L) e só busca as 24 h de histórico
/// quando chega a vez da tag, ~17 min depois (TAG33 no ciclo de 14:54 teve
/// 17,2 min). O histórico já traz pontos posteriores a L, o validador recusa
/// a entrada e o ensemble devolve nota 0 sem anomalia.
///
/// Referência ("como o replay"): o mesmo histórico terminando antes de L.
/// Isolamento: histórico em memória, SQLite em memória, sem Bridge.
/// </summary>
public class HistoricoCausalTests
{
    private const string Cliente = "PK2";
    private const string Tag = "TAG33";
    private const string Tipo = "perfil-comportamental";

    // Minuto da leitura do lote e instante da busca do histórico.
    private static readonly DateTime Leitura = new(2026, 11, 12, 14, 37, 0);
    private static readonly DateTime BuscaAtrasada = Leitura.AddMinutes(17);

    // Série de 1 ponto/min de Leitura - 6 h até Leitura + 4 h (cobre a busca
    // atrasada e o caso de fuso). Degrau de +60 a partir de Leitura - 5 min,
    // para a leitura ter nota > 0.
    private static readonly LeituraBruta[] Serie =
        Enumerable.Range(-360, 360 + 240 + 1)
            .Select(i => new LeituraBruta
            {
                DataHora = Leitura.AddMinutes(i),
                Valor = (float)(600
                    + 15 * Math.Sin(i / 9.0)
                    + (i % 7) * 0.9
                    + (i >= -5 ? 60 : 0))
            })
            .ToArray();

    private static readonly float ValorLeitura =
        Serie.Single(x => x.DataHora == Leitura).Valor;

    // ------------------------------------------------------------------
    // T1: caso de 12/11 (falha sem a correção)
    // ------------------------------------------------------------------

    [Fact]
    public async Task T1_LoteComHistoricoBuscado17MinDepois_ChaveLigada_AvaliaComHistoricoAnteriorALeitura()
    {
        var referencia = await PrepararEDetectar(
            new JanelaBusca(Serie, Leitura, incluiFim: false), causal: null);
        var atual = await PrepararEDetectar(
            new JanelaBusca(Serie, BuscaAtrasada, incluiFim: true), Ligada());

        Assert.True(atual.Validacao.IsValid, string.Join(" | ", atual.Validacao.Errors));
        Assert.True(atual.Input.Historico[^1].DataHora < Leitura);
        AssertMesmoHistorico(referencia.Input, atual.Input);

        Assert.True(referencia.Resultado.ScoreNormalizado > 0);
        Assert.Equal(referencia.Resultado.ScoreNormalizado, atual.Resultado.ScoreNormalizado);
        Assert.Equal(referencia.Resultado.EhAnomalia, atual.Resultado.EhAnomalia);
    }

    // ------------------------------------------------------------------
    // T2: chave desligada = comportamento atual (entrada inválida, nota 0)
    // ------------------------------------------------------------------

    [Fact]
    public async Task T2_ChaveDesligadaOuAusente_MantemEntradaInvalidaENotaZero()
    {
        foreach (var causal in new[] { null, Options.Create(new HistoricoCausalOptions()) })
        {
            var atual = await PrepararEDetectar(
                new JanelaBusca(Serie, BuscaAtrasada, incluiFim: true), causal);

            Assert.False(atual.Validacao.IsValid);
            Assert.Contains(atual.Validacao.Errors,
                e => e.Contains("anterior ao último ponto do histórico"));
            Assert.Equal(BuscaAtrasada, atual.Input.Historico[^1].DataHora);
            Assert.Equal(0, atual.Resultado.ScoreNormalizado);
            Assert.False(atual.Resultado.EhAnomalia);
        }
    }

    // ------------------------------------------------------------------
    // T3: o ponto do próprio minuto sai (recorte com "<")
    // ------------------------------------------------------------------

    [Fact]
    public async Task T3_PontoDoProprioMinuto_ChaveLigada_SaiDoHistorico()
    {
        var referencia = await PrepararEDetectar(
            new JanelaBusca(Serie, Leitura, incluiFim: false), causal: null);
        var desligada = await PrepararEDetectar(
            new JanelaBusca(Serie, Leitura, incluiFim: true), causal: null);
        var ligada = await PrepararEDetectar(
            new JanelaBusca(Serie, Leitura, incluiFim: true), Ligada());

        // Hoje: o ponto de L fica no histórico (só aviso do validador).
        Assert.True(desligada.Validacao.IsValid);
        Assert.Equal(Leitura, desligada.Input.Historico[^1].DataHora);

        // Com a chave: igual ao replay (histórico só antes de L).
        Assert.True(ligada.Validacao.IsValid);
        AssertMesmoHistorico(referencia.Input, ligada.Input);
        Assert.Equal(referencia.Resultado.ScoreNormalizado, ligada.Resultado.ScoreNormalizado);
    }

    // ------------------------------------------------------------------
    // T4: além da tolerância (relógio ou fuso) não recorta
    // ------------------------------------------------------------------

    [Fact]
    public async Task T4_PontosAlemDaTolerancia_NaoRecorta_ValidadorRecusaComoHoje()
    {
        var fuso = new JanelaBusca(Serie, Leitura.AddHours(3), incluiFim: true);

        var padrao = await PrepararEDetectar(fuso, Ligada());
        Assert.False(padrao.Validacao.IsValid);
        Assert.Equal(Leitura.AddHours(3), padrao.Input.Historico[^1].DataHora);
        Assert.Equal(0, padrao.Resultado.ScoreNormalizado);

        // A tolerância é o limite: com 200 min, o mesmo histórico é recortado.
        var larga = await PrepararEDetectar(fuso, Ligada(toleranciaMinutos: 200));
        Assert.True(larga.Validacao.IsValid);
        Assert.True(larga.Input.Historico[^1].DataHora < Leitura);
    }

    [Theory]
    [InlineData(30, true)]   // exatamente na tolerância: recorta
    [InlineData(31, false)]  // acima: não recorta
    public async Task T4_LimiteDaTolerancia(int minutosDepois, bool recorta)
    {
        var atual = await PrepararEDetectar(
            new JanelaBusca(Serie, Leitura.AddMinutes(minutosDepois), incluiFim: true), Ligada());

        Assert.Equal(recorta, atual.Validacao.IsValid);
        Assert.Equal(recorta, atual.Input.Historico[^1].DataHora < Leitura);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task T4_ToleranciaInvalida_NaoRecorta(double tolerancia)
    {
        var atual = await PrepararEDetectar(
            new JanelaBusca(Serie, BuscaAtrasada, incluiFim: true), Ligada(tolerancia));

        Assert.False(atual.Validacao.IsValid);
        Assert.Equal(BuscaAtrasada, atual.Input.Historico[^1].DataHora);
    }

    // ------------------------------------------------------------------
    // T5: nada a recortar = mesma lista, mesma nota
    // ------------------------------------------------------------------

    [Fact]
    public async Task T5_SemPontoPosterior_ChaveLigadaOuDesligada_MesmoResultado()
    {
        var fonte = new JanelaBusca(Serie, Leitura, incluiFim: false);

        var desligada = await PrepararEDetectar(fonte, causal: null);
        var ligada = await PrepararEDetectar(fonte, Ligada());

        AssertMesmoHistorico(desligada.Input, ligada.Input);
        Assert.Equal(desligada.Resultado.ScoreNormalizado, ligada.Resultado.ScoreNormalizado);
        Assert.Equal(desligada.Input.MediaHistorica, ligada.Input.MediaHistorica);
        Assert.Equal(desligada.Input.QualidadeHistorico.OriginalPoints,
            ligada.Input.QualidadeHistorico.OriginalPoints);
    }

    // ------------------------------------------------------------------
    // T1b: cadeia inteira até a gravação em AnomaliaDetectada
    // ------------------------------------------------------------------

    [Fact]
    public async Task T1b_DetectarComValorAtual_ChaveLigada_GravaANotaDaReferencia()
    {
        var referencia = await DetectarPeloMonitor(
            new JanelaBusca(Serie, Leitura, incluiFim: false), causal: null);
        var desligada = await DetectarPeloMonitor(
            new JanelaBusca(Serie, BuscaAtrasada, incluiFim: true), causal: null);
        var ligada = await DetectarPeloMonitor(
            new JanelaBusca(Serie, BuscaAtrasada, incluiFim: true), Ligada());

        // Hoje (P31): nota 0, sem anomalia.
        Assert.Equal(0, desligada.Score);
        Assert.False(desligada.EhAnomalia);

        // Com a chave: o que o replay gravaria.
        Assert.True(referencia.Score > 0);
        Assert.Equal(referencia.Score, ligada.Score);
        Assert.Equal(referencia.EhAnomalia, ligada.EhAnomalia);
    }

    // ------------------------------------------------------------------
    // T6: opções e injeção de dependências
    // ------------------------------------------------------------------

    [Fact]
    public void T6_Options_PadraoDesligadoCom30Min()
    {
        var options = new HistoricoCausalOptions();

        Assert.False(options.Habilitado);
        Assert.Equal(30, options.ToleranciaMinutos);
        Assert.Equal("AnomalyDetection:HistoricoCausal", HistoricoCausalOptions.SectionName);
    }

    [Fact]
    public void T6_Options_SemSecao_Desligado()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var options = provider.GetRequiredService<IOptions<HistoricoCausalOptions>>().Value;

        Assert.False(options.Habilitado);
        Assert.Equal(30, options.ToleranciaMinutos);
    }

    [Fact]
    public void T6_Options_LidaDaSecao()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["AnomalyDetection:HistoricoCausal:Habilitado"] = "true",
            ["AnomalyDetection:HistoricoCausal:ToleranciaMinutos"] = "45"
        });

        var options = provider.GetRequiredService<IOptions<HistoricoCausalOptions>>().Value;

        Assert.True(options.Habilitado);
        Assert.Equal(45, options.ToleranciaMinutos);
    }

    [Fact]
    public void T6_AppSettings_NaoTemSecaoHistoricoCausal()
    {
        // O padrão vem do código: o appsettings.json não pode ligar a chave.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(FindAppSettings(), optional: false)
            .Build();

        Assert.False(configuration.GetSection(HistoricoCausalOptions.SectionName).Exists());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public async Task T6_DI_PreparacaoResolvidaSegueAChave(string? habilitado, bool recorta)
    {
        var values = new Dictionary<string, string?>();
        if (habilitado is not null)
            values["AnomalyDetection:HistoricoCausal:Habilitado"] = habilitado;

        using var provider = BuildProvider(values, services =>
        {
            services.AddSingleton<IHistoricoService>(
                new JanelaBusca(Serie, BuscaAtrasada, incluiFim: true));
            services.AddScoped<PredictionDetectionPreparationService>();
        });
        using var scope = provider.CreateScope();

        var preparation = scope.ServiceProvider
            .GetRequiredService<PredictionDetectionPreparationService>();
        var result = await preparation.PrepararAsync(Request(), Perfil(), Tipo, Leitura);

        Assert.Equal(recorta, result.EntradaDetector.Historico[^1].DataHora < Leitura);
    }

    // ------------------------------------------------------------------

    private static IOptions<HistoricoCausalOptions> Ligada(
        double toleranciaMinutos = HistoricoCausalOptions.ToleranciaMinutosPadrao) =>
        Options.Create(new HistoricoCausalOptions
        {
            Habilitado = true,
            ToleranciaMinutos = toleranciaMinutos
        });

    private static AnomaliaRequest Request() => new()
    {
        ClienteId = Cliente,
        TagName = Tag,
        Valor = ValorLeitura,
        DataHora = Leitura
    };

    private static TagPerfilIa Perfil() => new()
    {
        ClienteId = Cliente,
        TagName = Tag,
        Media = 600,
        DesvioPadrao = 20,
        VariacaoMinimaRelevante = 5,
        DeadbandSugerido = 5,
        ConfiancaDeadband = 75,
        VariacaoMinimaCalculadaAutomaticamente = true,
        DataTreinamento = Leitura.AddDays(-1)
    };

    private static AnomalyDetectionOptions DetectionOptions() => new();

    // Os 6 detectores na ordem da injeção de dependências (como o replay).
    private static EnsembleDetectionService Ensemble()
    {
        var options = DetectionOptions();
        return new EnsembleDetectionService(
            new IAnomalyDetector[]
            {
                new StatisticalDetector(options),
                new MadDetector(options),
                new EwmaDetector(options),
                new RateOfChangeDetector(options),
                new PersistenceDetector(options),
                new LevelShiftDetector(options)
            },
            Options.Create(options),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());
    }

    private sealed record Execucao(
        DetectorInput Input,
        DetectionInputValidationResult Validacao,
        DetectorResult Resultado);

    private static async Task<Execucao> PrepararEDetectar(
        IHistoricoService history,
        IOptions<HistoricoCausalOptions>? causal)
    {
        var prepared = await new PredictionDetectionPreparationService(
                history, historicoCausal: causal)
            .PrepararAsync(Request(), Perfil(), Tipo, Leitura);

        var input = prepared.EntradaDetector;

        return new Execucao(
            input,
            DetectionInputValidator.Validate(input),
            Ensemble().Detectar(input));
    }

    private static void AssertMesmoHistorico(DetectorInput esperado, DetectorInput atual)
    {
        Assert.Equal(
            esperado.Historico.Select(p => (p.DataHora, p.Valor)),
            atual.Historico.Select(p => (p.DataHora, p.Valor)));
    }

    /// <summary>
    /// Caminho do monitor: CurrentValuePredictionService.DetectarComValorAtual
    /// com o valor do lote, PredictionService real e gravação em SQLite em
    /// memória. O BridgeValorAtualService não é usado por esse método (nulo:
    /// se fosse chamado, falharia em vez de acessar o Bridge).
    /// </summary>
    private static async Task<AnomaliaDetectada> DetectarPeloMonitor(
        IHistoricoService history,
        IOptions<HistoricoCausalOptions>? causal)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        db.TagsIaConfig.Add(new TagIaConfig { ClienteId = Cliente, TagName = Tag, IaAtiva = true });
        db.PerfisIa.Add(Perfil());
        await db.SaveChangesAsync();

        var options = DetectionOptions();
        var decision = new PredictionDecisionOrchestrationService(
            Ensemble(),
            new PredictionInterpretationService(
                new AnomalyInterpretationContextBuilder(new TagContextoIaService(db)),
                new InterpretacaoFixa()),
            new RuntimeFunctionalDecisionContextProvider(
                new RuntimeFunctionalDecisionStateStore(), Options.Create(options)),
            new PredictionRuntimeDecisionService(
                new RuntimeFunctionalDecisionCandidateFactory(),
                new RuntimeAnomalyDecisionResolver(),
                new RuntimeAnomalyDecisionOptions { Enabled = false },
                new RuntimeDecisionMetrics()));
        var postDecision = new PredictionPostDecisionAnalysisService(
            new AnaliseRelacionadasService(db, null!),
            new PredictionRiskAnalysisService(new RiskScoreService(), new ExplicacaoRiscoService()),
            new PredictionTrendAnalysisService(
                new TendenciaRiscoService(db), new TendenciaValorService(history)),
            new FeedbackAnomaliaAnaliseService(db));
        var finalization = new PredictionFinalizationService(
            new AnomaliaHistoricoService(db),
            new PredictionResponseBuilder(
                new PredictionDependencyMapper(new InterpretacaoDependenciasService())));
        var prediction = new PredictionService(
            new PredictionContextLoader(db, new ProcessoAnaliseService(db)),
            postDecision, finalization, decision,
            new PredictionDetectionPreparationService(history, historicoCausal: causal),
            multivariateShadowRuntimeCoordinator: null);

        var monitor = new CurrentValuePredictionService(null!, prediction);
        await monitor.DetectarComValorAtual(Cliente, Tag, new ValorAtualTagResponse
        {
            TagName = Tag,
            Valor = ValorLeitura,
            DataHora = Leitura,
            Encontrado = true
        });

        return await db.AnomaliasDetectadas.AsNoTracking().SingleAsync();
    }

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> values,
        Action<IServiceCollection>? extra = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        AnomalyDetectionServiceCollectionExtensions
            .AddAnomalyDetectionConfiguration(services, configuration);
        extra?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static string FindAppSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "appsettings.json");
            if (File.Exists(candidate) &&
                File.Exists(Path.Combine(directory.FullName, "IAApi.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("appsettings.json da API não encontrado.");
    }

    /// <summary>
    /// Como GET /historico?horas=24 do Bridge no instante <c>fim</c>:
    /// [fim - 24 h, fim], com ou sem o ponto de <c>fim</c>.
    /// </summary>
    private sealed class JanelaBusca(
        LeituraBruta[] serie,
        DateTime fim,
        bool incluiFim) : IHistoricoService
    {
        public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag) =>
            serie
                .Where(x => x.DataHora >= fim.AddHours(-24) &&
                            (x.DataHora < fim || (incluiFim && x.DataHora == fim)))
                .Select(x => new LeituraBruta { DataHora = x.DataHora, Valor = x.Valor })
                .ToList();

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId, string tagName, DateTime inicio, DateTime fimPeriodo) =>
            serie
                .Where(x => x.DataHora >= inicio && x.DataHora <= fimPeriodo)
                .Select(x => new LeituraBruta { DataHora = x.DataHora, Valor = x.Valor })
                .ToList();
    }

    private sealed class InterpretacaoFixa : IAnomalyInterpretationService
    {
        public Task<AnomalyInterpretationResult> InterpretarAsync(
            AnomalyInterpretationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AnomalyInterpretationResult
            {
                Resumo = "Teste",
                InterpretacaoTecnica = "Teste",
                Severidade = "Normal",
                ModeloUtilizado = "Teste"
            });
    }
}
