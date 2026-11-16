using IAApi.IntelligenceLab.Observability;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests.Detection;

/// <summary>
/// BACKLOG 33, passo 1: evidência independente fora da média ponderada,
/// combinada por "OU". Só detectores falsos: nenhum detector real com
/// esse papel existe ainda (passos 2 e 4).
/// </summary>
public class EnsembleIndependentEvidenceTests
{
    // ------------------------------------------------------------------
    // Não dilui: a média dos outros detectores não muda
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(55)]
    [InlineData(100)]
    public void NaoConfirmada_MediaEDecisaoIdenticasAoEnsembleSemEla(
        double scoreIndependente)
    {
        var semEla = CriarEnsemble(
                CriarDetector("A", 100, 0.15),
                CriarDetector("B", 100, 0.15),
                CriarDetector("C", 100, 0.15),
                CriarDetector("D", 0, 0.30))
            .Detectar(CriarInput());

        var comEla = CriarEnsemble(
                CriarDetector("A", 100, 0.15),
                CriarDetector("B", 100, 0.15),
                CriarDetector("C", 100, 0.15),
                CriarDetector("D", 0, 0.30),
                CriarIndependente("Ind", scoreIndependente, confirma: false, peso: 1))
            .Detectar(CriarInput());

        // Z+MAD+EWMA saturados = exatamente 60 (P07): a evidência
        // independente não pode puxar isso para baixo.
        Assert.Equal(60, semEla.ScoreNormalizado, precision: 10);
        Assert.Equal(semEla.ScoreNormalizado, comEla.ScoreNormalizado);
        Assert.Equal(semEla.Score, comEla.Score);
        Assert.Equal(semEla.EhAnomalia, comEla.EhAnomalia);
        Assert.Equal(semEla.AggregationPolicy, comEla.AggregationPolicy);
        Assert.Equal(semEla.Metricas["somaPesosAplicaveis"], comEla.Metricas["somaPesosAplicaveis"]);
        Assert.Equal(semEla.Metricas["quantidadeDetectoresAplicaveis"],
            comEla.Metricas["quantidadeDetectoresAplicaveis"]);
        Assert.Equal(semEla.Metricas["quantidadeAlertas"], comEla.Metricas["quantidadeAlertas"]);
    }

    [Fact]
    public void Aplicavel_FicaNosDetalhesForaDaAgregacao()
    {
        var resultado = CriarEnsemble(
                CriarDetector("A", 40, 1),
                CriarIndependente("Ind", 100, confirma: true, peso: 0.5))
            .Detectar(CriarInput());

        var detalhe = resultado.DetalhesDetectores.Single(x => x.Nome == "Ind");

        Assert.True(detalhe.Aplicavel);
        Assert.True(detalhe.VotouAnomalia);
        Assert.Equal(DetectorEvidenceRole.IndependentEvidence, detalhe.EvidenceRole);
        Assert.Equal(DetectorDiagnosticStatus.Executed, detalhe.ExecutionStatus);
        Assert.Equal(false, detalhe.ParticipatedInAggregation);
        Assert.Equal(DetectorDiagnosticReasonCodes.ExcludedIndependentEvidence,
            detalhe.AggregationExclusionReasonCode);
        Assert.Equal(0d, detalhe.EffectiveWeight!.Value);
        Assert.Equal(0d, detalhe.WeightedNumeratorContribution!.Value);
        Assert.Equal(0.5d, detalhe.ConfiguredWeight!.Value);

        // A soma dos pesos efetivos continua sendo só a da média.
        Assert.Equal(1d, resultado.Metricas["somaPesosAplicaveis"]);
    }

    // ------------------------------------------------------------------
    // "OU": só acrescenta alarme; nota = max(média, nota do detector)
    // ------------------------------------------------------------------

    [Fact]
    public void Confirmada_MediaAbaixoDoLimiar_ViraAnomaliaComNotaDoDetector()
    {
        var resultado = CriarEnsemble(
                CriarDetector("A", 30, 1),
                CriarIndependente("Ind", 100, confirma: true))
            .Detectar(CriarInput());

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(100, resultado.ScoreNormalizado, precision: 10);
        Assert.Equal(5, resultado.Score, precision: 10);
        Assert.Equal("WeightedMeanOrIndependentEvidence", resultado.AggregationPolicy);
        Assert.Equal(100, resultado.Metricas["scoreFinalNormalizado"], precision: 10);
        Assert.Equal(30, resultado.Metricas["scoreMediaPonderada"], precision: 10);
        Assert.Equal(1, resultado.Metricas["evidenciaIndependenteConfirmada"]);
        Assert.Equal(1, resultado.Metricas["quantidadeEvidenciasIndependentes"]);
        Assert.Equal(1, resultado.Metricas["quantidadeEvidenciasIndependentesConfirmadas"]);
        Assert.Equal(100, resultado.Metricas["scoreEvidenciaIndependenteMaximo"], precision: 10);
    }

    [Fact]
    public void Confirmada_MediaMaiorQueNotaDoDetector_MantemAMedia()
    {
        var resultado = CriarEnsemble(
                CriarDetector("A", 80, 1),
                CriarIndependente("Ind", 70, confirma: true))
            .Detectar(CriarInput());

        Assert.True(resultado.EhAnomalia);
        Assert.Equal(80, resultado.ScoreNormalizado, precision: 10);
        Assert.Equal("WeightedMeanOrIndependentEvidence", resultado.AggregationPolicy);
    }

    [Fact]
    public void Confirmada_VariasEvidencias_UsaAMaiorNotaConfirmada()
    {
        var resultado = CriarEnsemble(
                CriarDetector("A", 10, 1),
                CriarIndependente("Ind1", 75, confirma: true),
                CriarIndependente("Ind2", 95, confirma: false),
                CriarIndependente("Ind3", 85, confirma: true))
            .Detectar(CriarInput());

        Assert.True(resultado.EhAnomalia);
        Assert.Equal(85, resultado.ScoreNormalizado, precision: 10);
        Assert.Equal(3, resultado.Metricas["quantidadeEvidenciasIndependentes"]);
        Assert.Equal(2, resultado.Metricas["quantidadeEvidenciasIndependentesConfirmadas"]);
        Assert.Equal(95, resultado.Metricas["scoreEvidenciaIndependenteMaximo"], precision: 10);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(250)]
    [InlineData(-10)]
    public void Confirmada_NotaInvalidaOuForaDaFaixa_NotaFinalFicaEntre0E100(
        double scoreIndependente)
    {
        var resultado = CriarEnsemble(
                CriarDetector("A", 20, 1),
                CriarIndependente("Ind", scoreIndependente, confirma: true))
            .Detectar(CriarInput());

        Assert.True(resultado.EhAnomalia);
        Assert.True(double.IsFinite(resultado.ScoreNormalizado));
        Assert.InRange(resultado.ScoreNormalizado, 20, 100);
    }

    [Fact]
    public void NaoConfirmada_NuncaRemoveAlarmeDaMedia()
    {
        var resultado = CriarEnsemble(
                CriarDetector("A", 75, 1),
                CriarIndependente("Ind", 0, confirma: false))
            .Detectar(CriarInput());

        Assert.True(resultado.EhAnomalia);
        Assert.Equal(75, resultado.ScoreNormalizado, precision: 10);
        Assert.Equal("WeightedMeanAllApplicable", resultado.AggregationPolicy);
        Assert.Equal(0, resultado.Metricas["evidenciaIndependenteConfirmada"]);
        Assert.Equal(75, resultado.Metricas["scoreMediaPonderada"], precision: 10);
    }

    [Fact]
    public void Confirmada_ComPoliticaTemporalExperimental_SomaSufixoEPreservaMotivo()
    {
        var mad = CriarDetector("Mediana e MAD", 80, 1);
        var temporal = new FakeDetector("Persistência", 1, _ => new DetectorResult
        {
            Detector = "Persistência", Aplicavel = true, EhAnomalia = true,
            Score = 5, ScoreNormalizado = 100, Peso = 1,
            EvidenceRole = DetectorEvidenceRole.TemporalConfirmation,
            UnderlyingEvidenceKind = UnderlyingEvidenceKind.Mad,
            TemporalConfirmation = new TemporalConfirmationSnapshot
            {
                Status = TemporalConfirmationStatus.Confirmed,
                UnderlyingEvidenceKind = UnderlyingEvidenceKind.Mad
            }
        });
        var options = new AnomalyDetectionOptions
        {
            ExperimentalPersistenceTemporalConfirmationEnabled = true
        };

        var resultado = CriarEnsemble(
                options,
                mad,
                temporal,
                CriarIndependente("Ind", 90, confirma: true))
            .Detectar(CriarInput());

        Assert.Equal(90, resultado.ScoreNormalizado, precision: 10);
        Assert.Equal(80, resultado.Metricas["scoreMediaPonderada"], precision: 10);
        Assert.Equal(
            "WeightedMeanExcludeConfirmedMadTemporalConfirmation+IndependentEvidenceOr",
            resultado.AggregationPolicy);
        Assert.Equal(
            DetectorDiagnosticReasonCodes.ExcludedIndependentEvidence,
            resultado.DetalhesDetectores.Single(x => x.Nome == "Ind")
                .AggregationExclusionReasonCode);
        Assert.Equal(
            DetectorDiagnosticReasonCodes.ExcludedByExperimentalTemporalConfirmationPolicy,
            resultado.DetalhesDetectores.Single(x => x.Nome == "Persistência")
                .AggregationExclusionReasonCode);
    }

    // ------------------------------------------------------------------
    // Sem média (nenhum outro detector aplicável)
    // ------------------------------------------------------------------

    [Fact]
    public void SomenteEvidenciaIndependente_Confirmada_DecideSozinhaSemExcecaoDePeso()
    {
        var resultado = CriarEnsemble(
                CriarNaoAplicavel("A"),
                CriarIndependente("Ind", 100, confirma: true, peso: 0))
            .Detectar(CriarInput());

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(100, resultado.ScoreNormalizado, precision: 10);
        Assert.Equal("IndependentEvidenceOnly", resultado.AggregationPolicy);
        Assert.Equal(0, resultado.Metricas["quantidadeDetectoresAplicaveis"]);
        Assert.Equal(0, resultado.Metricas["somaPesosAplicaveis"]);
        Assert.Equal(100, resultado.Metricas["scoreFinalNormalizado"], precision: 10);
        Assert.Equal(0, resultado.Metricas["scoreMediaPonderada"]);
    }

    [Fact]
    public void SomenteEvidenciaIndependente_NaoConfirmada_MantemNoApplicableDetectors()
    {
        var resultado = CriarEnsemble(
                CriarNaoAplicavel("A"),
                CriarIndependente("Ind", 40, confirma: false))
            .Detectar(CriarInput());

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.ScoreNormalizado);
        Assert.Equal("NoApplicableDetectors", resultado.AggregationPolicy);
        Assert.Equal(1, resultado.Metricas["quantidadeEvidenciasIndependentes"]);
        Assert.Equal(0, resultado.Metricas["evidenciaIndependenteConfirmada"]);
    }

    // ------------------------------------------------------------------
    // Sem evidência independente aplicável: saída como antes
    // ------------------------------------------------------------------

    [Fact]
    public void SemEvidenciaIndependente_NaoAcrescentaMetricas()
    {
        var resultado = CriarEnsemble(
                CriarDetector("A", 70, 1),
                CriarDetector("B", 10, 1))
            .Detectar(CriarInput());

        Assert.Equal(12, resultado.Metricas.Count);
        Assert.DoesNotContain("quantidadeEvidenciasIndependentes", resultado.Metricas.Keys);
        Assert.DoesNotContain("scoreMediaPonderada", resultado.Metricas.Keys);
        Assert.Equal("WeightedMeanAllApplicable", resultado.AggregationPolicy);
        Assert.All(resultado.DetalhesDetectores,
            x => Assert.Null(x.AggregationExclusionReasonCode));
    }

    [Fact]
    public void SemDetectoresAplicaveis_NaoAcrescentaMetricas()
    {
        var resultado = CriarEnsemble(CriarNaoAplicavel("A"))
            .Detectar(CriarInput());

        Assert.Equal("NoApplicableDetectors", resultado.AggregationPolicy);
        Assert.Equal(12, resultado.Metricas.Count);
    }

    [Fact]
    public void EvidenciaIndependenteNaoAplicavel_EIgnorada()
    {
        var naoAplicavel = new FakeDetector("Ind", 1, _ => new DetectorResult
        {
            Detector = "Ind",
            Aplicavel = false,
            EhAnomalia = true,
            ScoreNormalizado = 100,
            Peso = 1,
            EvidenceRole = DetectorEvidenceRole.IndependentEvidence
        });

        var resultado = CriarEnsemble(
                CriarDetector("A", 30, 1),
                naoAplicavel)
            .Detectar(CriarInput());

        Assert.False(resultado.EhAnomalia);
        Assert.Equal(30, resultado.ScoreNormalizado, precision: 10);
        Assert.Equal(12, resultado.Metricas.Count);
        Assert.Null(resultado.DetalhesDetectores.Single(x => x.Nome == "Ind")
            .AggregationExclusionReasonCode);
    }

    [Fact]
    public void TagDiscretaDeAltaConfianca_BloqueiaTambemAEvidenciaIndependente()
    {
        // A política de aplicabilidade é a mesma para todos os detectores:
        // estados e comandos não passam pelo "OU" (BACKLOG 33.C, tipo do sinal).
        var independente = CriarIndependente("Ind", 100, confirma: true);

        var resultado = CriarEnsemble(
                CriarDetector("A", 10, 1),
                independente)
            .Detectar(CriarInput(tipoTag: "digital"));

        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, independente.Chamadas);
        Assert.Equal(DetectorDiagnosticReasonCodes.BlockedByApplicabilityPolicy,
            resultado.DetalhesDetectores.Single(x => x.Nome == "Ind").ExecutionReasonCode);
    }

    // ------------------------------------------------------------------
    // Política de participação e família V2
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PoliticaDeParticipacao_ExcluiEvidenciaIndependente(
        bool experimental,
        bool madPositivo)
    {
        var resultado = new DetectorResult
        {
            Detector = "Ind",
            Aplicavel = true,
            EhAnomalia = true,
            ScoreNormalizado = 100,
            Peso = 1,
            EvidenceRole = DetectorEvidenceRole.IndependentEvidence
        };

        Assert.False(EnsembleEvidenceParticipationPolicy
            .ParticipatesInStatisticalAggregation(resultado, experimental, madPositivo));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AdaptadorV2_EvidenciaIndependente_FamiliaDataQuality(bool aplicavel)
    {
        var item = new DetectorDebugItem
        {
            Nome = "Nome qualquer",
            Aplicavel = aplicavel,
            VotouAnomalia = aplicavel,
            ScoreNormalizado = aplicavel ? 100 : 0,
            ScoreOriginal = aplicavel ? 5 : 0,
            Peso = 0.5,
            Explicacao = "teste",
            EvidenceRole = DetectorEvidenceRole.IndependentEvidence
        };

        var v2 = new LegacyDetectorResultV2Adapter().Convert(item, 60);

        Assert.Equal(DetectorFamily.DataQuality, v2.Family);
        Assert.DoesNotContain(v2.Validar(), erro => erro.Contains("família"));
    }

    [Fact]
    public void AdaptadorV2_DetectorEstatistico_FamiliaInalterada()
    {
        var item = new DetectorDebugItem
        {
            Nome = "Z-Score",
            Aplicavel = true,
            ScoreNormalizado = 50,
            ScoreOriginal = 2.5,
            Peso = 0.2,
            Explicacao = "teste"
        };

        Assert.Equal(
            DetectorFamily.StatisticalDeviation,
            new LegacyDetectorResultV2Adapter().Convert(item, 60).Family);
    }

    // ------------------------------------------------------------------
    // Chaves (desligadas por padrão) e validação
    // ------------------------------------------------------------------

    [Fact]
    public void Opcoes_SemSecao_DesligadasEComListaVazia()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var congelado = provider.GetRequiredService<IOptions<ValorCongeladoOptions>>().Value;
        var ruido = provider.GetRequiredService<IOptions<RuidoExcessivoOptions>>().Value;

        Assert.False(congelado.Habilitado);
        Assert.Empty(congelado.ClientesHabilitados);
        Assert.False(congelado.EstaHabilitadoPara("PK2"));
        Assert.False(ruido.Habilitado);
        Assert.Empty(ruido.ClientesHabilitados);
        Assert.False(ruido.EstaHabilitadoPara("PK2"));
    }

    [Fact]
    public void Opcoes_LidasDaSecao_IndependentesEntreSi()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["AnomalyDetection:ValorCongelado:Habilitado"] = "true",
            ["AnomalyDetection:ValorCongelado:ClientesHabilitados:0"] = "PK2"
        });

        var congelado = provider.GetRequiredService<IOptions<ValorCongeladoOptions>>().Value;
        var ruido = provider.GetRequiredService<IOptions<RuidoExcessivoOptions>>().Value;

        Assert.True(congelado.EstaHabilitadoPara("PK2"));
        Assert.False(congelado.EstaHabilitadoPara("PK7"));
        Assert.False(ruido.Habilitado);
        Assert.False(ruido.EstaHabilitadoPara("PK2"));
    }

    [Theory]
    [InlineData(false, new[] { "PK2" }, "PK2", false)]
    [InlineData(true, new string[0], "PK2", false)]
    [InlineData(true, new[] { "PK2" }, "PK2", true)]
    [InlineData(true, new[] { " pk2 " }, "PK2", true)]
    [InlineData(true, new[] { "PK2" }, " pk2", true)]
    [InlineData(true, new[] { "PK2" }, "PK7", false)]
    [InlineData(true, new[] { "PK2" }, null, false)]
    [InlineData(true, new[] { "PK2" }, "  ", false)]
    public void EstaHabilitadoPara_ChaveEListaDeClientes(
        bool habilitado,
        string[] clientes,
        string? cliente,
        bool esperado)
    {
        var options = new ValorCongeladoOptions
        {
            Habilitado = habilitado,
            ClientesHabilitados = clientes.ToList()
        };

        Assert.Equal(esperado, options.EstaHabilitadoPara(cliente));
    }

    [Fact]
    public void Validador_ListaVaziaValida_ItemEmBrancoOuListaNulaInvalidos()
    {
        var validador = new EvidenciaIndependenteOptionsValidator<RuidoExcessivoOptions>();

        Assert.True(validador.Validate(null, new RuidoExcessivoOptions()).Succeeded);
        Assert.True(validador.Validate(null, new RuidoExcessivoOptions
        {
            Habilitado = true,
            ClientesHabilitados = ["PK2"]
        }).Succeeded);
        Assert.True(validador.Validate(null, new RuidoExcessivoOptions
        {
            ClientesHabilitados = ["PK2", " "]
        }).Failed);
        Assert.True(validador.Validate(null, new RuidoExcessivoOptions
        {
            ClientesHabilitados = null!
        }).Failed);
    }

    [Fact]
    public void Validador_RegistradoNaDi_RecusaItemEmBranco()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AnomalyDetection:ValorCongelado:ClientesHabilitados:0"] = " "
            })
            .Build();

        var services = new ServiceCollection();
        services.AddAnomalyDetectionConfiguration(configuration);
        services.AddAnomalyDetectionValidation();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ValorCongeladoOptions>>().Value);
        Assert.False(
            provider.GetRequiredService<IOptions<RuidoExcessivoOptions>>().Value.Habilitado);
    }

    [Fact]
    public void AppSettings_NaoTemAsSecoes_PadraoVemDoCodigo()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(FindAppSettings(), optional: false)
            .Build();

        Assert.False(configuration.GetSection(ValorCongeladoOptions.SectionName).Exists());
        Assert.False(configuration.GetSection(RuidoExcessivoOptions.SectionName).Exists());
    }

    // ------------------------------------------------------------------

    private static FakeDetector CriarDetector(
        string nome,
        double score,
        double peso) =>
        new(nome, peso, _ => new DetectorResult
        {
            Detector = nome,
            Aplicavel = true,
            EhAnomalia = score >= 60,
            Score = score / 20,
            ScoreNormalizado = score,
            Peso = peso,
            Explicacao = $"Resultado simulado de {nome}"
        });

    private static FakeDetector CriarIndependente(
        string nome,
        double score,
        bool confirma,
        double peso = 0.5) =>
        new(nome, peso, _ => new DetectorResult
        {
            Detector = nome,
            Aplicavel = true,
            EhAnomalia = confirma,
            Score = score / 20,
            ScoreNormalizado = score,
            Peso = peso,
            EvidenceRole = DetectorEvidenceRole.IndependentEvidence,
            Explicacao = $"Evidência independente simulada de {nome}"
        });

    private static FakeDetector CriarNaoAplicavel(string nome) =>
        new(nome, 1, _ => new DetectorResult
        {
            Detector = nome,
            Aplicavel = false,
            Peso = 1
        });

    private static EnsembleDetectionService CriarEnsemble(
        params IAnomalyDetector[] detectores) =>
        CriarEnsemble(new AnomalyDetectionOptions(), detectores);

    private static EnsembleDetectionService CriarEnsemble(
        AnomalyDetectionOptions options,
        params IAnomalyDetector[] detectores) =>
        new(
            detectores,
            Options.Create(options),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());

    private static DetectorInput CriarInput(string tipoTag = "")
    {
        var inicio = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = tipoTag,
            ValorAtual = 101,
            DataHoraAtual = inicio.AddMinutes(5),
            MediaHistorica = 100,
            DesvioPadraoHistorico = 2,
            Historico = Enumerable.Range(0, 5)
                .Select(i => new HistoricoDetectorPoint
                {
                    DataHora = inicio.AddMinutes(i),
                    Valor = 99.5 + i * 0.4
                })
                .ToArray()
        };
    }

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddAnomalyDetectionConfiguration(configuration);
        services.AddAnomalyDetectionValidation();

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

    private sealed class FakeDetector : IAnomalyDetector
    {
        private readonly Func<DetectorInput, DetectorResult> _executar;

        public FakeDetector(
            string nome,
            double peso,
            Func<DetectorInput, DetectorResult> executar)
        {
            Nome = nome;
            Peso = peso;
            _executar = executar;
        }

        public string Nome { get; }
        public double Peso { get; }
        public int Chamadas { get; private set; }

        public DetectorResult Detectar(DetectorInput input)
        {
            Chamadas++;
            return _executar(input);
        }
    }
}
