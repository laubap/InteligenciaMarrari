using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeFunctionalDecisionContextProviderTests
{
    [Fact]
    public void PrimeiraLeituraNormal_ProduzContextoSemInventarIncidente()
    {
        var provider = CreateProvider();
        var input = Input("C1", "TAG1", 100);
        var legacy = Legacy(false);

        var context = provider.Evaluate(input, legacy);

        Assert.Null(context.IncidentId);
        Assert.False(context.CandidateBaselineReady);
        Assert.False(context.OperationalBaselineChanged);
        Assert.False(context.Promotion);
    }

    [Fact]
    public void EstadosDeTagsDiferentes_FicamIsolados()
    {
        var store = new RuntimeFunctionalDecisionStateStore();
        var provider = CreateProvider(store);

        provider.Evaluate(Input("C1", "TAG-A", 100), Legacy(false));
        provider.Evaluate(Input("C1", "TAG-B", 100), Legacy(false));

        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void MesmaTag_ReutilizaMesmoEstado()
    {
        var store = new RuntimeFunctionalDecisionStateStore();
        var provider = CreateProvider(store);

        provider.Evaluate(Input("C1", "TAG-A", 100), Legacy(false));
        provider.Evaluate(Input("C1", "TAG-A", 101), Legacy(false));

        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Provider_NaoMutaDecisaoLegacy()
    {
        var provider = CreateProvider();
        var legacy = Legacy(true);

        _ = provider.Evaluate(Input("C1", "TAG1", 1000), legacy);

        Assert.True(legacy.EhAnomalia);
    }

    [Fact]
    public void GapAntigo_ForaDoTrechoContinuoRecente_NaoSuspendeShadow()
    {
        var provider = CreateProvider();

        var start =
            new DateTime(
                2026, 8, 21, 12, 0, 0,
                DateTimeKind.Utc);

        var history =
            new List<HistoricoDetectorPoint>();

        // Bloco antigo.
        for (var i = 0; i < 10; i++)
        {
            history.Add(new HistoricoDetectorPoint
            {
                DataHora = start.AddMinutes(i),
                Valor = 100 + (i % 2)
            });
        }

        // Gap crítico antigo de 8 minutos.
        var resumedAt =
            start.AddMinutes(17);

        // Trecho recente contínuo suficiente.
        for (var i = 0; i < 30; i++)
        {
            history.Add(new HistoricoDetectorPoint
            {
                DataHora = resumedAt.AddMinutes(i),
                Valor = 100 + (i % 2)
            });
        }

        var input =
            new DetectorInput
            {
                ClienteId = "C1",
                TagName = "TAG-GAP-ANTIGO",
                TipoTag = "temperatura",
                ValorAtual = 100,
                DataHoraAtual =
                    resumedAt.AddMinutes(31),
                MediaHistorica =
                    history.Average(x => x.Valor),
                Historico = history
            };

        Assert.True(
            input.QualidadeHistorico.HasCriticalGap);

        var context =
            provider.Evaluate(
                input,
                Legacy(false));

        Assert.False(context.CriticalGap);
        Assert.NotEqual(
            "SuspendedByDataQuality",
            context.RegimeState);
    }

    [Fact]
    public void GapRecente_ComTrechoPosteriorInsuficiente_PreservaSuspensao()
    {
        var provider = CreateProvider();

        var start =
            new DateTime(
                2026, 8, 21, 12, 0, 0,
                DateTimeKind.Utc);

        var history =
            Enumerable.Range(0, 30)
                .Select(i =>
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            start.AddMinutes(i),
                        Valor = 100 + (i % 2)
                    })
                .ToList();

        // Gap crítico recente, seguido por somente 3 pontos.
        var resumedAt =
            start.AddMinutes(37);

        for (var i = 0; i < 3; i++)
        {
            history.Add(new HistoricoDetectorPoint
            {
                DataHora = resumedAt.AddMinutes(i),
                Valor = 100
            });
        }

        var input =
            new DetectorInput
            {
                ClienteId = "C1",
                TagName = "TAG-GAP-RECENTE",
                TipoTag = "temperatura",
                ValorAtual = 100,
                DataHoraAtual =
                    resumedAt.AddMinutes(4),
                MediaHistorica =
                    history.Average(x => x.Valor),
                Historico = history
            };

        Assert.True(
            input.QualidadeHistorico.HasCriticalGap);

        var context =
            provider.Evaluate(
                input,
                Legacy(true));

        Assert.True(context.CriticalGap);
        Assert.Equal(
            "SuspendedByDataQuality",
            context.RegimeState);
    }

    [Fact]
    public void IncreasingNoise_UmMissNaoEncerraEpisodioAtivo()
    {
        var store =
            new RuntimeFunctionalDecisionStateStore();

        var provider =
            CreateProvider(store);

        var getOrCreate =
            typeof(RuntimeFunctionalDecisionStateStore)
                .GetMethod(
                    "GetOrCreate",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(getOrCreate);

        var state =
            getOrCreate.Invoke(
                store,
                new object[]
                {
                    "C1",
                    "TAG-NOISE"
                });

        Assert.NotNull(state);

        var activeProperty =
            state.GetType()
                .GetProperty(
                    "IncreasingNoiseActive",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);

        Assert.NotNull(activeProperty);

        activeProperty.SetValue(
            state,
            true);

        var context =
            provider.Evaluate(
                Input(
                    "C1",
                    "TAG-NOISE",
                    100),
                Legacy(false));

        Assert.False(
            context.IncreasingNoise);

        Assert.True(
            (bool)activeProperty.GetValue(state)!);
    }

    [Fact]
    public void IncreasingNoise_DoisMissesEncerramEpisodio()
    {
        var store =
            new RuntimeFunctionalDecisionStateStore();

        var provider =
            CreateProvider(store);

        var getOrCreate =
            typeof(RuntimeFunctionalDecisionStateStore)
                .GetMethod(
                    "GetOrCreate",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(getOrCreate);

        var state =
            getOrCreate.Invoke(
                store,
                new object[]
                {
                    "C1",
                    "TAG-NOISE-END"
                });

        Assert.NotNull(state);

        var activeProperty =
            state.GetType()
                .GetProperty("IncreasingNoiseActive");

        Assert.NotNull(activeProperty);

        activeProperty.SetValue(
            state,
            true);

        provider.Evaluate(
            Input(
                "C1",
                "TAG-NOISE-END",
                100),
            Legacy(false));

        Assert.True(
            (bool)activeProperty.GetValue(state)!);

        provider.Evaluate(
            Input(
                "C1",
                "TAG-NOISE-END",
                100),
            Legacy(false));

        Assert.False(
            (bool)activeProperty.GetValue(state)!);
    }

    [Fact]
    public void CriticalGap_PreservaMetadadosDeDeadbandNoTrechoContinuo()
    {
        var start =
            new DateTime(
                2026, 8, 21, 12, 0, 0,
                DateTimeKind.Utc);

        var history =
            new List<HistoricoDetectorPoint>();

        // Bloco anterior ao gap.
        for (var i = 0; i < 10; i++)
        {
            history.Add(
                new HistoricoDetectorPoint
                {
                    DataHora = start.AddMinutes(i),
                    Valor = 100 + (i % 2)
                });
        }

        // Gap crítico (> 5x a cadência mediana de 1 minuto).
        var resumedAt =
            start.AddMinutes(20);

        // Trecho contínuo posterior suficiente para o runtime reconstruir o input.
        for (var i = 0; i < 20; i++)
        {
            history.Add(
                new HistoricoDetectorPoint
                {
                    DataHora = resumedAt.AddMinutes(i),
                    Valor = 100 + (i % 2)
                });
        }

        var input =
            new DetectorInput
            {
                ClienteId = "C1",
                TagName = "TAG-DEADBAND-GAP",
                TipoTag = "temperatura",

                ValorAtual = 101,

                DataHoraAtual =
                    resumedAt.AddMinutes(21),

                MediaHistorica =
                    history.Average(x => x.Valor),

                DesvioPadraoHistorico = 1,

                VariacaoMinimaRelevante = 16,
                ConfiancaVariacaoMinima = 94,
                VariacaoMinimaDefinidaPeloCliente = true,

                Historico = history
            };

        Assert.True(
            input.QualidadeHistorico.HasCriticalGap);

        var method =
            typeof(RuntimeFunctionalDecisionContextProvider)
                .GetMethod(
                    "CreateContiguousRuntimeInput",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var contiguousInput =
            Assert.IsType<DetectorInput>(
                method.Invoke(
                    null,
                    new object[] { input }));

        Assert.NotSame(
            input,
            contiguousInput);

        Assert.Equal(
            16,
            contiguousInput.VariacaoMinimaRelevante);

        Assert.Equal(
            94,
            contiguousInput.ConfiancaVariacaoMinima);

        Assert.True(
            contiguousInput.VariacaoMinimaDefinidaPeloCliente);
    }

    private static RuntimeFunctionalDecisionContextProvider CreateProvider(
        RuntimeFunctionalDecisionStateStore? store = null)
    {
        return new RuntimeFunctionalDecisionContextProvider(
            store ?? new RuntimeFunctionalDecisionStateStore(),
            Options.Create(new AnomalyDetectionOptions()));
    }

    private static DetectorResult Legacy(bool anomaly) => new()
    {
        EhAnomalia = anomaly,
        Score = anomaly ? 4 : 0
    };

    private static DetectorInput Input(
        string cliente,
        string tag,
        double value)
    {
        var start =
            new DateTime(
                2026, 8, 21, 12, 0, 0,
                DateTimeKind.Utc);

        var history =
            Enumerable.Range(0, 40)
                .Select(i =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = start.AddMinutes(i),
                        Valor = 100 + (i % 3)
                    })
                .ToArray();

        return new DetectorInput
        {
            ClienteId = cliente,
            TagName = tag,
            TipoTag = "temperatura",
            ValorAtual = value,
            DataHoraAtual = start.AddMinutes(41),
            MediaHistorica = history.Average(x => x.Valor),
            Historico = history
        };
    }
}