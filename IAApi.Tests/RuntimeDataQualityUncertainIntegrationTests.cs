using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeDataQualityUncertainIntegrationTests
{
    [Fact]
    public void GapCriticoRecente_DeveVirarDataQualityUncertain()
    {
        var provider =
            new RuntimeFunctionalDecisionContextProvider(
                new RuntimeFunctionalDecisionStateStore(),
                Options.Create(new AnomalyDetectionOptions()));

        var factory =
            new RuntimeFunctionalDecisionCandidateFactory();

        var start =
            new DateTime(
                2026, 8, 21, 12, 0, 0,
                DateTimeKind.Utc);

        var history =
            Enumerable.Range(0, 30)
                .Select(i =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = start.AddMinutes(i),
                        Valor = 100 + (i % 2)
                    })
                .ToList();

        // Gap crítico recente.
        var resumedAt =
            start.AddMinutes(37);

        // Apenas 3 pontos depois do gap:
        // insuficiente para sanitizar o histórico.
        for (var i = 0; i < 3; i++)
        {
            history.Add(
                new HistoricoDetectorPoint
                {
                    DataHora = resumedAt.AddMinutes(i),
                    Valor = 100
                });
        }

        var input =
            new DetectorInput
            {
                ClienteId = "C1",
                TagName = "TAG-DATA-QUALITY",
                TipoTag = "temperatura",
                ValorAtual = 5000,
                DataHoraAtual =
                    resumedAt.AddMinutes(4),
                MediaHistorica =
                    history.Average(x => x.Valor),
                Historico = history
            };

        var legacy =
            new DetectorResult
            {
                EhAnomalia = true,
                Score = 4
            };

        var context =
            provider.Evaluate(
                input,
                legacy);

        Assert.True(context.CriticalGap);
        Assert.Equal(
            "SuspendedByDataQuality",
            context.RegimeState);

       var candidate =
    factory.Create(
        legacy.EhAnomalia,
        context);

        Assert.Equal(
            RuntimeFunctionalDecisionSources
                .DataQualityUncertain,
            candidate.Source);

        Assert.True(candidate.EmitAnomaly);
        Assert.True(candidate.EmitNewEvidence);

        Assert.Contains(
            "CriticalDataQualityGap",
            candidate.Reasons);
    }
}
