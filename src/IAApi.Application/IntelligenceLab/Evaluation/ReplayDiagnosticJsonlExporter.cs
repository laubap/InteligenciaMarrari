using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.IntelligenceLab.Observability;

namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// Exportacao exclusiva de replay offline. Valida a correlacao ja capturada;
/// nao calcula scores, votos ou decisoes.
/// </summary>
public static class ReplayDiagnosticJsonlExporter
{
    public const string SchemaVersion = "intelligence-lab-replay-diagnostics/v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public static IReadOnlyList<ReplayDiagnosticJsonlEntry> CreateEntries(
        string runId,
        string scenarioId,
        IReadOnlyList<AiReadingEvaluationResult> readings,
        IReadOnlyList<IntelligenceLabReadingDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ValidateCorrelation(readings, diagnostics);

        var entries = new ReplayDiagnosticJsonlEntry[readings.Count];
        for (var index = 0; index < readings.Count; index++)
        {
            var reading = readings[index];
            var diagnostic = diagnostics[index];
            entries[index] = new ReplayDiagnosticJsonlEntry(
                SchemaVersion,
                runId,
                scenarioId,
                reading.TestClientId,
                reading.ReadingId,
                reading.TagName,
                reading.TimestampUtc,
                reading.DiagnosticId!.Value,
                diagnostic.ReadingValue,
                diagnostic);
        }

        return Array.AsReadOnly(entries);
    }

    /// <summary>
    /// A ordem do buffer deve ser a mesma das leituras do replay. Reordenar
    /// esconderia uma falha de captura e poderia associar a evidencia errada.
    /// </summary>
    public static void ValidateCorrelation(
        IReadOnlyList<AiReadingEvaluationResult> readings,
        IReadOnlyList<IntelligenceLabReadingDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (readings.Count != diagnostics.Count)
            throw new InvalidOperationException(
                $"Integridade do replay falhou: {readings.Count} leituras e {diagnostics.Count} diagnosticos.");

        var diagnosticIds = new HashSet<Guid>();
        DateTimeOffset? previousTimestamp = null;
        for (var index = 0; index < readings.Count; index++)
        {
            var reading = readings[index] ?? throw new InvalidOperationException(
                $"Integridade do replay falhou: leitura nula na posicao {index}.");
            var diagnostic = diagnostics[index] ?? throw new InvalidOperationException(
                $"Integridade do replay falhou: diagnostico nulo na posicao {index}.");
            var diagnosticId = reading.DiagnosticId;

            if (diagnosticId is null || diagnosticId.Value == Guid.Empty)
                throw new InvalidOperationException(
                    $"Integridade do replay falhou: leitura {index + 1} sem DiagnosticId.");
            if (!diagnosticIds.Add(diagnostic.Context.DiagnosticId))
                throw new InvalidOperationException(
                    $"Integridade do replay falhou: DiagnosticId duplicado na posicao {index + 1}.");
            if (reading.TimestampUtc.Offset != TimeSpan.Zero ||
                (previousTimestamp is not null && reading.TimestampUtc <= previousTimestamp.Value))
                throw new InvalidOperationException(
                    "Integridade do replay falhou: leituras nao estao em ordem UTC estritamente crescente.");
            if (diagnostic.Context.DiagnosticId != diagnosticId.Value)
                throw new InvalidOperationException(
                    $"Integridade do replay falhou: DiagnosticId divergente na leitura {index + 1}.");
            if (!string.Equals(diagnostic.ClienteId, reading.TestClientId, StringComparison.Ordinal) ||
                !string.Equals(diagnostic.TagName, reading.TagName, StringComparison.Ordinal) ||
                diagnostic.ReadingTimestampUtc != reading.TimestampUtc)
                throw new InvalidOperationException(
                    $"Integridade do replay falhou: cliente, tag ou horario divergente na leitura {index + 1}.");

            previousTimestamp = reading.TimestampUtc;
        }
    }

    public static async Task WriteAsync(
        string path,
        IReadOnlyList<ReplayDiagnosticJsonlEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(entries);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ??
            throw new InvalidOperationException("Arquivo de diagnosticos sem diretorio valido.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, bufferSize: 16 * 1024, useAsync: true))
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken);
                    await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
                }

                await stream.FlushAsync(cancellationToken);
            }

            // Mesma pasta garante rename no mesmo volume; o arquivo final so
            // aparece depois que todas as linhas foram gravadas com sucesso.
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

/// <summary>
/// Uma linha JSONL: identificacao da leitura para correlacao e o contrato de
/// diagnostico original, inclusive nulls e informacoes parciais.
/// </summary>
public sealed record ReplayDiagnosticJsonlEntry(
    string SchemaVersion,
    string RunId,
    string ScenarioId,
    string TestClientId,
    string ReadingId,
    string TagName,
    DateTimeOffset TimestampUtc,
    Guid DiagnosticId,
    double ObservedValue,
    IntelligenceLabReadingDiagnostic Diagnostic);
