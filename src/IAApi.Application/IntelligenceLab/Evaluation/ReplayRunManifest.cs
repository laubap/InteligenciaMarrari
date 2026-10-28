using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// Evidência reproduzível de um replay offline. Não contém caminhos absolutos,
/// argumentos de processo, ambiente ou configurações que possam carregar segredo.
/// </summary>
public enum ManifestVerificationStatus
{
    Verified,
    Partial,
    Unknown
}

public enum ManifestResourceExecutionStatus
{
    Executed,
    NotExecuted,
    Unknown
}

public sealed record ReplayManifestInput(
    string Name,
    string Sha256,
    long ByteLength,
    DateTimeOffset ReadStartedAtUtc,
    DateTimeOffset ReadCompletedAtUtc,
    ManifestVerificationStatus VerificationStatus);

public sealed record ReplayManifestGit(
    string? Commit,
    bool? HasUncommittedChanges,
    ManifestVerificationStatus VerificationStatus,
    string Evidence);

public sealed record ReplayManifestConfiguration(
    string Mode,
    IReadOnlyList<string> EnabledDetectors,
    IReadOnlyList<string> DisabledResources,
    IReadOnlyDictionary<string, object> EffectiveSettings,
    string FingerprintSha256,
    ManifestVerificationStatus VerificationStatus);

public sealed record ReplayManifestContract(
    string Component,
    string Version,
    ManifestVerificationStatus VerificationStatus,
    string Applicability);

public sealed record ReplayManifestResource(
    string Component,
    ManifestResourceExecutionStatus ExecutionStatus,
    ManifestVerificationStatus VerificationStatus,
    string Evidence,
    string? Sha256 = null);

public sealed record ReplayManifestArtifact(
    string Name,
    string Sha256,
    long ByteLength,
    ManifestVerificationStatus VerificationStatus);

public sealed record IntelligenceLabRunManifest(
    string SchemaVersion,
    string RunId,
    DateTimeOffset ExecutionStartedAtUtc,
    DateTimeOffset ExecutionCompletedAtUtc,
    ManifestVerificationStatus ReproducibilityStatus,
    string ReproducibilityLimitation,
    ReplayManifestInput TrainingInput,
    ReplayManifestInput EvaluationInput,
    ReplayManifestGit Git,
    ReplayManifestConfiguration Configuration,
    IReadOnlyList<ReplayManifestContract> Contracts,
    IReadOnlyList<ReplayManifestResource> Resources,
    IReadOnlyList<ReplayManifestArtifact> Artifacts,
    string SelfHashPolicy);

public sealed record ReplayRunManifestRequest(
    string RunId,
    DateTimeOffset ExecutionStartedAtUtc,
    DateTimeOffset ExecutionCompletedAtUtc,
    ReplayManifestInput TrainingInput,
    ReplayManifestInput EvaluationInput,
    ReplayManifestGit Git,
    ReplayManifestConfiguration Configuration,
    IReadOnlyList<ReplayManifestContract> Contracts,
    IReadOnlyList<ReplayManifestResource> Resources,
    IReadOnlyList<ReplayManifestArtifact> Artifacts,
    bool ExportsCompleted);

public static class ReplayRunManifest
{
    public const string SchemaVersion = "intelligence-lab-run-manifest/v1";
    public const string SelfHashPolicy = "ManifestExcludedToAvoidCircularHash";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Sha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string CreateConfigurationFingerprint(
        string mode,
        IReadOnlyList<string> enabledDetectors,
        IReadOnlyList<string> disabledResources,
        IReadOnlyDictionary<string, object> effectiveSettings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(enabledDetectors);
        ArgumentNullException.ThrowIfNull(disabledResources);
        ArgumentNullException.ThrowIfNull(effectiveSettings);

        // A ordem em que os itens foram declarados não muda a operação do
        // replay. Canonicalizar evita fingerprints diferentes sem mudança real.
        var canonical = new
        {
            Mode = mode,
            EnabledDetectors = enabledDetectors.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            DisabledResources = disabledResources.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
EffectiveSettings = new SortedDictionary<string, object>(
    effectiveSettings.ToDictionary(
        item => item.Key,
        item => item.Value,
        StringComparer.Ordinal),
    StringComparer.Ordinal)
        };
        return Sha256Hex(JsonSerializer.SerializeToUtf8Bytes(canonical,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    /// <summary>
    /// Invalida somente o manifesto da pasta de saída antes de uma nova
    /// publicação. Se a exportação posterior falhar, nenhum manifesto antigo
    /// permanece para aparentar validar artefatos de outro ensaio.
    /// </summary>
    public static string PrepareOutputManifestPath(string outputFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        var folder = Path.GetFullPath(outputFolder);
        Directory.CreateDirectory(folder);
        var manifestPath = Path.Combine(folder, "intelligence-lab-run-manifest.json");
        if (File.Exists(manifestPath))
            File.Delete(manifestPath);
        return manifestPath;
    }

    public static IntelligenceLabRunManifest Create(ReplayRunManifestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        return new IntelligenceLabRunManifest(
            SchemaVersion,
            request.RunId,
            request.ExecutionStartedAtUtc.ToUniversalTime(),
            request.ExecutionCompletedAtUtc.ToUniversalTime(),
            ManifestVerificationStatus.Partial,
            request.ExportsCompleted
                ? "Dependencias de build e ambiente nao possuem evidencia hash verificavel; nao declarar reproducao completa."
                : "Exportacao de artefatos incompleta; o manifesto nao pode declarar execucao completa.",
            request.TrainingInput,
            request.EvaluationInput,
            request.Git,
            request.Configuration,
            request.Contracts,
            request.Resources,
            request.Artifacts,
            SelfHashPolicy);
    }

    public static string Serialize(IntelligenceLabRunManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        EnsureNoSensitiveInformation(json);
        return json;
    }

    public static void EnsureNoSensitiveInformation(string json)
    {
        using var document = JsonDocument.Parse(json);
        ValidateElement(document.RootElement);
    }

    private static void ValidateRequest(ReplayRunManifestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RunId) ||
            request.ExecutionStartedAtUtc.Offset != TimeSpan.Zero ||
            request.ExecutionCompletedAtUtc.Offset != TimeSpan.Zero ||
            request.ExecutionCompletedAtUtc < request.ExecutionStartedAtUtc)
            throw new ArgumentException("Manifesto possui identidade ou timestamps UTC invalidos.");

        if (!request.ExportsCompleted || request.Artifacts is null || request.Artifacts.Count == 0)
            throw new ArgumentException("Manifesto completo exige exportacao concluida e artefatos verificados.");

        foreach (var input in new[] { request.TrainingInput, request.EvaluationInput })
        {
            if (input is null || input.ReadStartedAtUtc.Offset != TimeSpan.Zero ||
                input.ReadCompletedAtUtc.Offset != TimeSpan.Zero ||
                input.ReadCompletedAtUtc < input.ReadStartedAtUtc || !IsSha256(input.Sha256))
                throw new ArgumentException("Entrada do manifesto sem hash SHA-256 ou horario UTC verificavel.");
        }

        if (request.Configuration is null || !IsSha256(request.Configuration.FingerprintSha256))
            throw new ArgumentException("Configuracao efetiva sem fingerprint SHA-256 verificavel.");
    }

    private static bool IsSha256(string? value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

    private static void ValidateElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (LooksSensitive(property.Name))
                        throw new InvalidOperationException("Manifesto contem um campo sensivel.");
                    ValidateElement(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    ValidateElement(item);
                break;
            case JsonValueKind.String:
                if (LooksSensitive(element.GetString() ?? string.Empty))
                    throw new InvalidOperationException("Manifesto contem valor potencialmente sensivel.");
                break;
        }
    }

    private static bool LooksSensitive(string value)
    {
        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty)
            .ToLowerInvariant();
        return normalized.Contains("password", StringComparison.Ordinal) ||
               normalized.Contains("credential", StringComparison.Ordinal) ||
               normalized.Contains("connectionstring", StringComparison.Ordinal) ||
               normalized.Contains("secret", StringComparison.Ordinal) ||
               normalized.Contains("token", StringComparison.Ordinal) ||
               normalized.Contains("apikey", StringComparison.Ordinal) ||
               normalized.Contains("authorization", StringComparison.Ordinal) ||
               normalized.Contains("authtoken", StringComparison.Ordinal) ||
               normalized.StartsWith("bearer ", StringComparison.Ordinal) ||
               normalized.Contains("server=", StringComparison.Ordinal);
    }
}

/// <summary>
/// Descobre o checkout da IAApi por marcadores locais e só então consulta Git.
/// Um checkout Git arbitrário do diretório atual não é evidência do código
/// executado pelo replay.
/// </summary>
public static class ReplayGitMetadataResolver
{
    public delegate string? GitCommandRunner(string repositoryRoot,
        IReadOnlyList<string> arguments);

    public static ReplayManifestGit Read(IEnumerable<string> candidatePaths,
        GitCommandRunner? runGit = null)
    {
        ArgumentNullException.ThrowIfNull(candidatePaths);
        runGit ??= RunGit;

        foreach (var root in FindIaApiRoots(candidatePaths))
        {
            var gitRoot = runGit(root, ["rev-parse", "--show-toplevel"]);
            if (string.IsNullOrWhiteSpace(gitRoot) || !SamePath(root, gitRoot))
                continue;

            var commit = runGit(root, ["rev-parse", "HEAD"]);
            var status = runGit(root, ["status", "--porcelain"]);
            if (string.IsNullOrWhiteSpace(commit) || status is null)
                continue;

            var dirty = status.Length > 0;
            return new ReplayManifestGit(commit, dirty,
                dirty ? ManifestVerificationStatus.Partial : ManifestVerificationStatus.Verified,
                dirty ? "Commit identified; local uncommitted changes detected." :
                    "Commit identified; local tree clean.");
        }

        return new ReplayManifestGit(null, null, ManifestVerificationStatus.Unknown,
            "IAApi Git repository could not be identified with confidence.");
    }

    private static IEnumerable<string> FindIaApiRoots(IEnumerable<string> candidatePaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidatePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var directory = File.Exists(candidate) ? Path.GetDirectoryName(candidate) : candidate;
            for (var current = directory is null ? null : new DirectoryInfo(Path.GetFullPath(directory));
                 current is not null;
                 current = current.Parent)
            {
                var path = current.FullName;
                if (!seen.Add(path) || !IsIaApiRepositoryRoot(path))
                    continue;
                yield return path;
            }
        }
    }

    private static bool IsIaApiRepositoryRoot(string path) =>
        File.Exists(Path.Combine(path, "IAApi.csproj")) &&
        File.Exists(Path.Combine(path, "tools", "IntelligenceLabReplay",
            "IAApi.IntelligenceLabReplay.csproj"));

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string? RunGit(string repositoryRoot, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = repositoryRoot
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);
            using var process = Process.Start(startInfo);
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
