using System.Text.Json;
using IAApi.IntelligenceLab.Evaluation;
using IAApi.IntelligenceLab.Observability;

namespace IAApi.Tests.IntelligenceLab;

public sealed class ReplayDiagnosticJsonlExporterTests
{
    private const string Client = "SIM-IL-REPLAY-PK7";
    private const string Tag = "PK7.TAG1";
    private static readonly DateTimeOffset Start =
        new(2026, 10, 27, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UmaLeitura_ExportaDiagnosticoCompletoCorrelacionado()
    {
        var (readings, diagnostics) = CreateCorrelated(1);
        var entries = ReplayDiagnosticJsonlExporter.CreateEntries("run-1", "scenario-1", readings, diagnostics);
        var path = Path.Combine(Path.GetTempPath(), $"diagnostics-{Guid.NewGuid():N}.jsonl");

        try
        {
            await ReplayDiagnosticJsonlExporter.WriteAsync(path, entries);

            var line = Assert.Single(await File.ReadAllLinesAsync(path));
            using var json = JsonDocument.Parse(line);
            Assert.Equal(entries[0].DiagnosticId.ToString(),
                json.RootElement.GetProperty("diagnosticId").GetString());
            Assert.Equal(Client, json.RootElement.GetProperty("testClientId").GetString());
            Assert.Equal(Tag, json.RootElement.GetProperty("tagName").GetString());
            Assert.Equal("Partial", json.RootElement.GetProperty("diagnostic")
                .GetProperty("completeness").GetProperty("status").GetString());
            Assert.True(json.RootElement.GetProperty("diagnostic")
                .TryGetProperty("baseline", out var baseline));
            Assert.Equal(JsonValueKind.Null, baseline.ValueKind);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void VinteLeituras_ProduzemVinteEntradasEmOrdemDeterministica()
    {
        var (readings, diagnostics) = CreateCorrelated(20);

        var entries = ReplayDiagnosticJsonlExporter.CreateEntries("run-20", "scenario-1", readings, diagnostics);

        Assert.Equal(20, entries.Count);
        Assert.Equal(readings.Select(x => x.TimestampUtc), entries.Select(x => x.TimestampUtc));
        Assert.Equal(diagnostics.Select(x => x.Context.DiagnosticId), entries.Select(x => x.DiagnosticId));
    }

    [Fact]
    public void DiagnosticoParcial_EPreservadoSemPreencherLacunas()
    {
        var (readings, diagnostics) = CreateCorrelated(1);

        var entry = Assert.Single(ReplayDiagnosticJsonlExporter.CreateEntries(
            "run-partial", "scenario-1", readings, diagnostics));

        Assert.Same(diagnostics[0], entry.Diagnostic);
        Assert.Equal(DiagnosticCompletenessStatus.Partial, entry.Diagnostic.Completeness.Status);
        Assert.Contains("Baseline", entry.Diagnostic.Completeness.MissingSections);
        Assert.Null(entry.Diagnostic.Baseline);
        Assert.Null(entry.Diagnostic.Ensemble);
    }

    [Fact]
    public void LeituraSemDiagnosticId_FalhaNaValidacaoDeIntegridade()
    {
        var (readings, diagnostics) = CreateCorrelated(1);
        readings[0] = readings[0] with { DiagnosticId = null };

        var failure = Assert.Throws<InvalidOperationException>(() =>
            ReplayDiagnosticJsonlExporter.CreateEntries("run", "scenario", readings, diagnostics));

        Assert.Contains("sem DiagnosticId", failure.Message);
    }

    [Fact]
    public void DiagnosticIdDuplicado_FalhaNaValidacaoDeIntegridade()
    {
        var (readings, diagnostics) = CreateCorrelated(2);
        var id = diagnostics[0].Context.DiagnosticId;
        diagnostics[1] = Diagnostic(id, Start.AddMinutes(1));
        readings[1] = readings[1] with { DiagnosticId = id };

        var failure = Assert.Throws<InvalidOperationException>(() =>
            ReplayDiagnosticJsonlExporter.CreateEntries("run", "scenario", readings, diagnostics));

        Assert.Contains("duplicado", failure.Message);
    }

    [Fact]
    public void DiagnosticoOrfaoOuForaDaOrdem_FalhaNaValidacaoDeIntegridade()
    {
        var (readings, diagnostics) = CreateCorrelated(2);

        var orphan = diagnostics.Append(Diagnostic(Guid.NewGuid(), Start.AddMinutes(2))).ToArray();
        var orphanFailure = Assert.Throws<InvalidOperationException>(() =>
            ReplayDiagnosticJsonlExporter.CreateEntries("run", "scenario", readings, orphan));
        Assert.Contains("leituras e 3 diagnosticos", orphanFailure.Message);

        (diagnostics[0], diagnostics[1]) = (diagnostics[1], diagnostics[0]);
        var orderFailure = Assert.Throws<InvalidOperationException>(() =>
            ReplayDiagnosticJsonlExporter.CreateEntries("run", "scenario", readings, diagnostics));
        Assert.Contains("DiagnosticId divergente", orderFailure.Message);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("tag")]
    [InlineData("timestamp")]
    public void ClienteTagOuHorarioDivergente_FalhaNaValidacao(string field)
    {
        var (readings, diagnostics) = CreateCorrelated(1);
        diagnostics[0] = field switch
        {
            "client" => Diagnostic(diagnostics[0].Context.DiagnosticId, Start, client: "SIM-OTHER"),
            "tag" => Diagnostic(diagnostics[0].Context.DiagnosticId, Start, tag: "PK7.OTHER"),
            _ => Diagnostic(diagnostics[0].Context.DiagnosticId, Start.AddMinutes(1))
        };

        var failure = Assert.Throws<InvalidOperationException>(() =>
            ReplayDiagnosticJsonlExporter.CreateEntries("run", "scenario", readings, diagnostics));

        Assert.Contains("cliente, tag ou horario", failure.Message);
    }

    [Fact]
    public async Task JsonlPreservaContribuicoesM3SemAlterarEnvelopeM1()
    {
        var id = Guid.NewGuid();
        var timestamp = Start;
        var reading = new AiReadingEvaluationResult(
            IndustrialEvaluationSchemas.ReadingV1, "run", "scenario", Client,
            "reading-00", Tag, timestamp, 80, true, id, "Legacy");
        var aggregation = new DetectorAggregationDiagnostic(
            "Z-Score", "StatisticalEvidence", registered: true, executed: true,
            applicable: true, votedAnomaly: true, normalizedScore: 80,
            configuredWeight: 0.2, effectiveWeight: 0.2,
            participatedInAggregation: true, weightedNumeratorContribution: 16,
            executionCompleted: true,
            executionReasonCode: DetectorDiagnosticReasonCodes.InvokedAndCompleted);
        var ensemble = new EnsembleDiagnostic(
            applicable: true, legacyAnomaly: true, originalScore: 4,
            normalizedScore: 80, threshold: 60, registeredDetectorCount: 1,
            applicableDetectorCount: 1, alertCount: 1, applicableWeightSum: 0.2,
            contributions: [aggregation], aggregationPolicy: "WeightedMeanAllApplicable");
        var diagnostic = Diagnostic(id, timestamp, ensemble: ensemble);
        var path = Path.Combine(Path.GetTempPath(), $"diagnostics-m3-{Guid.NewGuid():N}.jsonl");

        try
        {
            await ReplayDiagnosticJsonlExporter.WriteAsync(path,
                ReplayDiagnosticJsonlExporter.CreateEntries("run", "scenario", [reading], [diagnostic]));

            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var exported = json.RootElement.GetProperty("diagnostic").GetProperty("ensemble");
            Assert.Equal("WeightedMeanAllApplicable", exported
                .GetProperty("aggregationPolicy").GetString());
            Assert.Equal(16, Assert.Single(exported.GetProperty("contributions")
                .EnumerateArray()).GetProperty("weightedNumeratorContribution").GetDouble());
            Assert.Equal(DetectorDiagnosticReasonCodes.InvokedAndCompleted,
                Assert.Single(exported.GetProperty("contributions").EnumerateArray())
                    .GetProperty("executionReasonCode").GetString());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task FalhaDeFinalizacao_NaoSubstituiArquivoExistenteNemMutaDecisao()
    {
        var (readings, diagnostics) = CreateCorrelated(1);
        var entries = ReplayDiagnosticJsonlExporter.CreateEntries("run", "scenario", readings, diagnostics);
        var finalDecision = diagnostics[0].RuntimeDecision!.FinalAnomaly;
        var directoryTarget = Path.Combine(Path.GetTempPath(), $"diagnostics-directory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryTarget);

        try
        {
            var exception = await Record.ExceptionAsync(() =>
    ReplayDiagnosticJsonlExporter.WriteAsync(directoryTarget, entries));

Assert.True(
    exception is IOException or UnauthorizedAccessException,
    $"Exceção inesperada: {exception?.GetType().FullName}"
);

            Assert.True(Directory.Exists(directoryTarget));
            Assert.Equal(finalDecision, diagnostics[0].RuntimeDecision!.FinalAnomaly);
            var parent = Directory.GetParent(directoryTarget)!.FullName;
            Assert.Empty(Directory.GetFiles(parent,
                $".{Path.GetFileName(directoryTarget)}.*.tmp"));
        }
        finally
        {
            Directory.Delete(directoryTarget, recursive: true);
        }
    }

    private static (AiReadingEvaluationResult[] Readings, IntelligenceLabReadingDiagnostic[] Diagnostics)
        CreateCorrelated(int count)
    {
        var readings = new AiReadingEvaluationResult[count];
        var diagnostics = new IntelligenceLabReadingDiagnostic[count];
        for (var index = 0; index < count; index++)
        {
            var timestamp = Start.AddMinutes(index);
            var id = Guid.NewGuid();
            diagnostics[index] = Diagnostic(id, timestamp);
            readings[index] = new AiReadingEvaluationResult(
                IndustrialEvaluationSchemas.ReadingV1, "run", "scenario", Client,
                $"reading-{index:D2}", Tag, timestamp, null, index % 2 == 0, id, "Legacy");
        }

        return (readings, diagnostics);
    }

    private static IntelligenceLabReadingDiagnostic Diagnostic(
        Guid id,
        DateTimeOffset timestamp,
        string? client = null,
        string? tag = null,
        EnsembleDiagnostic? ensemble = null)
        => new(
            DiagnosticContext.Create("test-correlation", "run", timestamp, id),
            new DiagnosticVersionMetadata("test-api", "test-build", "test-commit", "test-config"),
            client ?? Client,
            tag ?? Tag,
            timestamp,
            123.45,
            new DiagnosticCompleteness(DiagnosticCompletenessStatus.Partial,
                ["Baseline", "Ensemble"], "Fixture parcial."),
            new DiagnosticTiming(TimeSpan.Zero),
            ensemble: ensemble,
            runtimeDecision: new RuntimeDecisionDiagnostic(
                legacyAnomaly: true, finalAnomaly: true, emitNewEvidence: false,
                usedLegacyFallback: true, source: "Legacy", reasons: [], temporalConfirmation: null));
}
