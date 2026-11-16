using IAApi.IntelligenceLab.Observability;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests.Detection;

/// <summary>
/// BACKLOG 33.C, passo 2: detector de valor congelado como evidência
/// independente. Séries sintéticas de 1 leitura/min; o histórico não inclui
/// a leitura atual (como em produção).
/// </summary>
public class ValorCongeladoDetectorTests
{
    private static readonly DateTime Inicio = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Unspecified);

    // ------------------------------------------------------------------
    // Detecção e tipo do sinal
    // ------------------------------------------------------------------

    [Fact]
    public void Inteiros14Valores_TipoUnknown_ConfirmaAos10Min()
    {
        // Como a TAG24 do CP300: inteiros de 445 a 458, sai Unknown.
        var input = Entrada(SerieInteira(120), congelado: 450, minutosCongelado: 10);

        Assert.Equal(TagBehaviorType.Unknown,
            input.ObterPerfilComportamental(new TagBehaviorProfileResolver()).Type);

        var resultado = Detector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(100, resultado.ScoreNormalizado);
        Assert.Equal(DetectorEvidenceRole.IndependentEvidence, resultado.EvidenceRole);
        Assert.Equal(ValorCongeladoDetector.NomeDetector, resultado.Detector);
        Assert.Equal(11, resultado.Metricas["pontosIguaisSeguidos"]);
        Assert.Equal(10, resultado.Metricas["duracaoCongeladaMinutos"], precision: 10);
        Assert.Equal(450, resultado.Metricas["valorCongelado"]);
        Assert.Equal(0, resultado.Metricas["repousoConhecido"]);
    }

    [Fact]
    public void Aos9Min_AplicavelSemConfirmar_NotaDeProgressoAbaixoDe100()
    {
        var resultado = Detector().Detectar(
            Entrada(SerieInteira(120), congelado: 450, minutosCongelado: 9));

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(90, resultado.ScoreNormalizado, precision: 10);
    }

    [Fact]
    public void SinalContinuoDecimal_Confirma()
    {
        var input = Entrada(SerieDecimal(120), congelado: 101.234, minutosCongelado: 12);

        Assert.Equal(TagBehaviorType.Continuous,
            input.ObterPerfilComportamental(new TagBehaviorProfileResolver()).Type);
        Assert.True(Detector().Detectar(input).EhAnomalia);
    }

    [Theory]
    [InlineData("digital")]
    [InlineData("setpoint")]
    [InlineData("contador")]
    public void EstadoSetpointOuContador_NaoAplicavel(string tipoTag)
    {
        var resultado = Detector().Detectar(
            Entrada(SerieInteira(120), congelado: 450, minutosCongelado: 20, tipoTag: tipoTag));

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(DetectorDiagnosticReasonCodes.SignalTypeNotSupported,
            resultado.NonApplicabilityReasonCode);
    }

    // ------------------------------------------------------------------
    // Guarda de "repouso conhecido" e variação anterior
    // ------------------------------------------------------------------

    [Fact]
    public void ZeroJaVistoParadoNoHistorico_RepousoConhecido_NaoAlarma()
    {
        // Bomba parada 30 min, depois operando 60 min, e para de novo.
        var anteriores = Enumerable.Repeat(0.0, 30).Concat(SerieInteira(60)).ToList();

        var resultado = Detector().Detectar(
            Entrada(anteriores, congelado: 0, minutosCongelado: 15));

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.ScoreNormalizado);
        Assert.Equal(1, resultado.Metricas["repousoConhecido"]);
        Assert.Equal(29, resultado.Metricas["maiorRepousoAnteriorMinutos"], precision: 10);
    }

    [Fact]
    public void ZeroSemRepousoLongoNoHistorico_Alarma()
    {
        // Só 5 leituras em 0 antes (4 min): não é repouso conhecido.
        var anteriores = Enumerable.Repeat(0.0, 5).Concat(SerieInteira(60)).ToList();

        var resultado = Detector().Detectar(
            Entrada(anteriores, congelado: 0, minutosCongelado: 15));

        Assert.True(resultado.EhAnomalia);
        Assert.Equal(4, resultado.Metricas["maiorRepousoAnteriorMinutos"], precision: 10);
    }

    [Fact]
    public void RepousoNoLimite_10MinAlarmaNao_9MinAlarma()
    {
        var repouso10 = Enumerable.Repeat(0.0, 11).Concat(SerieInteira(60)).ToList();
        var repouso9 = Enumerable.Repeat(0.0, 10).Concat(SerieInteira(60)).ToList();

        Assert.False(Detector().Detectar(Entrada(repouso10, congelado: 0, minutosCongelado: 15)).EhAnomalia);
        Assert.True(Detector().Detectar(Entrada(repouso9, congelado: 0, minutosCongelado: 15)).EhAnomalia);
    }

    [Fact]
    public void TagQuaseConstanteAntes_NaoAlarma()
    {
        // Degraus de 4 leituras iguais: 75 % dos passos sem variação (> 0,60).
        var anteriores = Enumerable.Range(0, 120)
            .Select(i => 100 + 0.5 * ((i / 4) % 7))
            .ToList();

        var resultado = Detector().Detectar(
            Entrada(anteriores, congelado: 120.25, minutosCongelado: 15));

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.ScoreNormalizado);
        Assert.True(resultado.Metricas["taxaPassosIguaisAntes"] > 0.60);
    }

    [Fact]
    public void HistoricoAnteriorInsuficiente_NaoAplicavel()
    {
        var resultado = Detector().Detectar(
            Entrada(SerieDecimal(5), congelado: 101.234, minutosCongelado: 12));

        Assert.False(resultado.Aplicavel);
        Assert.Equal(DetectorDiagnosticReasonCodes.FrozenValueNotApplicable,
            resultado.NonApplicabilityReasonCode);
    }

    // ------------------------------------------------------------------
    // Gaps (histórico do regime é concatenado)
    // ------------------------------------------------------------------

    [Fact]
    public void HistoricoComGapCritico_UsaUltimoTrechoContiguo_EConfirma()
    {
        var input = EntradaComTrechoAntigo(trechoAntigo: SerieInteira(60), congelado: 450);

        Assert.True(input.QualidadeHistorico.HasCriticalGap);

        var resultado = Detector().Detectar(input);

        Assert.True(resultado.EhAnomalia);
        Assert.True(resultado.Metricas["pontosHistoricoContiguo"] < input.Historico.Count);
    }

    [Fact]
    public void RepousoNoTrechoAntigo_AntesDoGap_TambemConta()
    {
        // O mesmo valor ficou parado 20 min num trecho antigo do regime.
        var antigo = SerieInteira(40).Concat(Enumerable.Repeat(450.0, 21)).ToList();
        var input = EntradaComTrechoAntigo(trechoAntigo: antigo, congelado: 450);

        var resultado = Detector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(1, resultado.Metricas["repousoConhecido"]);
    }

    [Fact]
    public void LeituraAtualSeparadaPorGapCritico_NaoAplicavel()
    {
        var historico = SerieInteira(120)
            .Select((v, i) => Ponto(Inicio.AddMinutes(i), v))
            .ToArray();

        var input = new DetectorInput
        {
            ClienteId = "PK2",
            TagName = "TAG24",
            ValorAtual = historico[^1].Valor,
            DataHoraAtual = historico[^1].DataHora.AddMinutes(30),
            MediaHistorica = 451.5,
            DesvioPadraoHistorico = 4,
            Historico = historico
        };

        var resultado = Detector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.Equal(DetectorDiagnosticReasonCodes.NoContiguousHistory,
            resultado.NonApplicabilityReasonCode);
    }

    // ------------------------------------------------------------------
    // Chave por cliente
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(false, "PK2")]
    [InlineData(true, "PK7")]
    public void ClienteNaoHabilitado_NaoAplicavel(bool habilitado, string cliente)
    {
        var detector = new ValorCongeladoDetector(
            new ValorCongeladoOptions { Habilitado = habilitado, ClientesHabilitados = ["PK2"] },
            new TagBehaviorProfileResolver());

        var resultado = detector.Detectar(
            Entrada(SerieInteira(120), congelado: 450, minutosCongelado: 20, cliente: cliente));

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(DetectorDiagnosticReasonCodes.ClientNotEnabledForDetector,
            resultado.NonApplicabilityReasonCode);
    }

    // ------------------------------------------------------------------
    // No ensemble real (P12: o congelamento no valor normal não alarmava)
    // ------------------------------------------------------------------

    [Fact]
    public void Ensemble_DetectoresLegados_SoAlarmaComOValorCongelado()
    {
        var options = new AnomalyDetectionOptions();
        IAnomalyDetector[] legados =
        [
            new StatisticalDetector(options),
            new MadDetector(options),
            new EwmaDetector(options),
            new RateOfChangeDetector(options),
            new PersistenceDetector(options),
            new LevelShiftDetector(options)
        ];

        var sem = Ensemble(options, legados)
            .Detectar(Entrada(SerieInteira(120), congelado: 450, minutosCongelado: 12));
        var com = Ensemble(options, [.. legados, Detector()])
            .Detectar(Entrada(SerieInteira(120), congelado: 450, minutosCongelado: 12));

        Assert.False(sem.EhAnomalia);
        Assert.True(com.EhAnomalia);
        Assert.Equal("WeightedMeanOrIndependentEvidence", com.AggregationPolicy);
        Assert.Equal(100, com.ScoreNormalizado);
        Assert.Equal(sem.ScoreNormalizado, com.Metricas["scoreMediaPonderada"], precision: 10);
        Assert.Equal(sem.Metricas["somaPesosAplicaveis"], com.Metricas["somaPesosAplicaveis"]);
    }

    // ------------------------------------------------------------------
    // Opções, validação e DI (chave desligada = não registrado)
    // ------------------------------------------------------------------

    [Fact]
    public void Opcoes_PadroesIguaisAosDoCalculadorExistente()
    {
        var options = new ValorCongeladoOptions();
        var calculador = new IAApi.Services.AnomalyDetection.Runtime.Regime.RegimeShadowOptions();

        Assert.False(options.Habilitado);
        Assert.Equal(calculador.FrozenMinimumPoints, options.MinimoPontos);
        Assert.Equal(calculador.FrozenMinimumMinutes, options.MinimoMinutos);
        Assert.Equal(calculador.FrozenPriorWindowPoints, options.JanelaAnteriorPontos);
        Assert.Equal(calculador.FrozenMaximumPriorZeroRate, options.TaxaMaximaPassosIguaisAntes);
        Assert.Equal(10, options.MinutosRepouso);
        Assert.Empty(options.ValidarParametros());
    }

    [Theory]
    [InlineData(nameof(ValorCongeladoOptions.MinimoPontos), 2)]
    [InlineData(nameof(ValorCongeladoOptions.MinimoMinutos), 0)]
    [InlineData(nameof(ValorCongeladoOptions.MinimoMinutos), double.NaN)]
    [InlineData(nameof(ValorCongeladoOptions.JanelaAnteriorPontos), 2)]
    [InlineData(nameof(ValorCongeladoOptions.TaxaMaximaPassosIguaisAntes), 1.5)]
    [InlineData(nameof(ValorCongeladoOptions.TaxaMaximaPassosIguaisAntes), -0.1)]
    [InlineData(nameof(ValorCongeladoOptions.MinutosRepouso), 0)]
    public void Validador_ParametroInvalido_Falha(string parametro, double valor)
    {
        var options = new ValorCongeladoOptions();
        var propriedade = typeof(ValorCongeladoOptions).GetProperty(parametro)!;
        propriedade.SetValue(options,
            propriedade.PropertyType == typeof(int) ? (object)(int)valor : valor);

        var resultado = new EvidenciaIndependenteOptionsValidator<ValorCongeladoOptions>()
            .Validate(null, options);

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures!, f => f.Contains(parametro));
    }

    [Fact]
    public void Di_ChaveDesligada_SoOsSeisDetectoresLegados()
    {
        var nomes = DetectoresRegistrados(new Dictionary<string, string?>());

        Assert.Equal(6, nomes.Count);
        Assert.DoesNotContain(ValorCongeladoDetector.NomeDetector, nomes);
    }

    [Fact]
    public void Di_ChaveLigada_RegistraOValorCongeladoDepoisDosLegados()
    {
        var nomes = DetectoresRegistrados(new Dictionary<string, string?>
        {
            ["AnomalyDetection:ValorCongelado:Habilitado"] = "true",
            ["AnomalyDetection:ValorCongelado:ClientesHabilitados:0"] = "PK2",
            ["AnomalyDetection:ValorCongelado:MinutosRepouso"] = "15"
        });

        Assert.Equal(7, nomes.Count);
        Assert.Equal(ValorCongeladoDetector.NomeDetector, nomes[^1]);
    }

    [Fact]
    public void Di_ChaveLigadaComParametroInvalido_Recusa()
    {
        Assert.Throws<OptionsValidationException>(() => DetectoresRegistrados(
            new Dictionary<string, string?>
            {
                ["AnomalyDetection:ValorCongelado:Habilitado"] = "true",
                ["AnomalyDetection:ValorCongelado:MinimoMinutos"] = "0"
            }));
    }

    // ------------------------------------------------------------------

    private static ValorCongeladoDetector Detector() =>
        new(
            new ValorCongeladoOptions { Habilitado = true, ClientesHabilitados = ["PK2"] },
            new TagBehaviorProfileResolver());

    private static EnsembleDetectionService Ensemble(
        AnomalyDetectionOptions options,
        IAnomalyDetector[] detectores) =>
        new(detectores, Options.Create(options),
            new TagBehaviorProfileResolver(), new DetectorApplicabilityPolicy());

    /// <summary>Inteiros 445–458 sem repetir o valor anterior (14 distintos).</summary>
    private static List<double> SerieInteira(int pontos) =>
        Enumerable.Range(0, pontos).Select(i => 445.0 + (i * 5) % 14).ToList();

    private static List<double> SerieDecimal(int pontos) =>
        Enumerable.Range(0, pontos)
            .Select(i => Math.Round(100 + 2 * Math.Sin(0.7 * i) + Math.Sin(1.9 * i), 3))
            .ToList();

    /// <summary>
    /// <paramref name="anteriores"/>, depois o valor <paramref name="congelado"/>
    /// por <paramref name="minutosCongelado"/> minutos (de t até t + m, m + 1
    /// leituras). A leitura atual é a última congelada; o histórico vai até a
    /// anterior.
    /// </summary>
    private static DetectorInput Entrada(
        IReadOnlyList<double> anteriores,
        double congelado,
        int minutosCongelado,
        string tipoTag = "",
        string cliente = "PK2")
    {
        Assert.NotEqual(congelado, anteriores[^1]);

        var valores = anteriores
            .Concat(Enumerable.Repeat(congelado, minutosCongelado + 1))
            .ToList();
        var pontos = valores
            .Select((v, i) => Ponto(Inicio.AddMinutes(i), v))
            .ToArray();

        return new DetectorInput
        {
            ClienteId = cliente,
            TagName = "TAG24",
            TipoTag = tipoTag,
            ValorAtual = pontos[^1].Valor,
            DataHoraAtual = pontos[^1].DataHora,
            MediaHistorica = anteriores.Average(),
            DesvioPadraoHistorico = 4,
            Historico = pontos[..^1]
        };
    }

    /// <summary>
    /// Trecho antigo, 3 h de gap (regime concatenado), depois 60 leituras
    /// variando e 12 min congelado em <paramref name="congelado"/>.
    /// </summary>
    private static DetectorInput EntradaComTrechoAntigo(
        IReadOnlyList<double> trechoAntigo,
        double congelado)
    {
        var antigo = trechoAntigo
            .Select((v, i) => Ponto(Inicio.AddMinutes(i), v));
        var inicioRecente = Inicio.AddMinutes(trechoAntigo.Count + 180);
        var recentes = SerieInteira(61).Skip(1)
            .Concat(Enumerable.Repeat(congelado, 13))
            .Select((v, i) => Ponto(inicioRecente.AddMinutes(i), v))
            .ToArray();
        var pontos = antigo.Concat(recentes).ToArray();

        return new DetectorInput
        {
            ClienteId = "PK2",
            TagName = "TAG24",
            ValorAtual = pontos[^1].Valor,
            DataHoraAtual = pontos[^1].DataHora,
            MediaHistorica = 451.5,
            DesvioPadraoHistorico = 4,
            Historico = pontos[..^1]
        };
    }

    private static HistoricoDetectorPoint Ponto(DateTime t, double v) =>
        new() { DataHora = t, Valor = v };

    private static List<string> DetectoresRegistrados(
        Dictionary<string, string?> valores)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build();

        var services = new ServiceCollection();
        services.AddAnomalyDetectionConfiguration(configuration);
        services.AddAnomalyDetectionCoreServices();
        services.AddLegacyAnomalyDetectionServices(configuration);
        services.AddAnomalyDetectionValidation();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        return scope.ServiceProvider
            .GetServices<IAnomalyDetector>()
            .Select(d => d.Nome)
            .ToList();
    }
}
