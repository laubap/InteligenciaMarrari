using System.Text;
using System.Text.Json;
using IAApi.IntelligenceLab.Evaluation;

namespace IAApi.Tests.IntelligenceLab;

public sealed class ReplayRunManifestTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MesmoConteudo_GeraHashEstavel_EConteudoAlteradoGeraOutroHash()
    {
        var first = Encoding.UTF8.GetBytes("[{\"tagName\":\"PK7.TAG1\",\"valor\":1}]");
        var identical = Encoding.UTF8.GetBytes("[{\"tagName\":\"PK7.TAG1\",\"valor\":1}]");
        var changed = Encoding.UTF8.GetBytes("[{\"tagName\":\"PK7.TAG1\",\"valor\":2}]");

        Assert.Equal(ReplayRunManifest.Sha256Hex(first), ReplayRunManifest.Sha256Hex(identical));
        Assert.NotEqual(ReplayRunManifest.Sha256Hex(first), ReplayRunManifest.Sha256Hex(changed));
    }

    [Fact]
    public void InformacaoGitAusente_MantemIdentidadeParcialESemDeclararReproducaoCompleta()
    {
        var manifest = ReplayRunManifest.Create(Request(git: new ReplayManifestGit(
            null, null, ManifestVerificationStatus.Unknown, "Git indisponivel.")));

        Assert.Equal(ManifestVerificationStatus.Partial, manifest.ReproducibilityStatus);
        Assert.Equal(ManifestVerificationStatus.Unknown, manifest.Git.VerificationStatus);
        Assert.Contains("nao declarar reproducao completa", manifest.ReproducibilityLimitation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfiguracaoEfetivaESeuFingerprintSaoRepresentadosDeterministicamente()
    {
        var settings = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["ensembleThreshold"] = 60d,
            ["runtimeFunctionalDecisionEnabled"] = false
        };
        var configuration = new ReplayManifestConfiguration(
            "OfflineIsolatedHistoricalReplay", ["Statistical", "MAD"],
            ["IsolationForest"], settings,
            ReplayRunManifest.CreateConfigurationFingerprint("OfflineIsolatedHistoricalReplay",
                ["Statistical", "MAD"], ["IsolationForest"], settings),
            ManifestVerificationStatus.Verified);
        var manifest = ReplayRunManifest.Create(Request(configuration: configuration));

        Assert.Equal("OfflineIsolatedHistoricalReplay", manifest.Configuration.Mode);
        Assert.False((bool)manifest.Configuration.EffectiveSettings["runtimeFunctionalDecisionEnabled"]);
        Assert.Equal(ReplayRunManifest.CreateConfigurationFingerprint(
                configuration.Mode, configuration.EnabledDetectors,
                configuration.DisabledResources, settings),
            manifest.Configuration.FingerprintSha256);

        var changedSettings = new SortedDictionary<string, object>(settings, StringComparer.Ordinal)
        {
            ["ensembleThreshold"] = 61d
        };
        Assert.NotEqual(manifest.Configuration.FingerprintSha256,
            ReplayRunManifest.CreateConfigurationFingerprint(configuration.Mode,
                configuration.EnabledDetectors, configuration.DisabledResources, changedSettings));

        var reordered = ReplayRunManifest.CreateConfigurationFingerprint(configuration.Mode,
            ["MAD", "Statistical"], ["IsolationForest"], settings);
        Assert.Equal(manifest.Configuration.FingerprintSha256, reordered);
        Assert.NotEqual(manifest.Configuration.FingerprintSha256,
            ReplayRunManifest.CreateConfigurationFingerprint("OtherMode",
                configuration.EnabledDetectors, configuration.DisabledResources, settings));
        Assert.NotEqual(manifest.Configuration.FingerprintSha256,
            ReplayRunManifest.CreateConfigurationFingerprint(configuration.Mode,
                ["Statistical"], configuration.DisabledResources, settings));
    }

    [Fact]
    public void SerializacaoRejeitaCredenciaisETokens()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReplayRunManifest.EnsureNoSensitiveInformation("{\"apiToken\":\"nao-exportar\"}"));
        Assert.Throws<InvalidOperationException>(() =>
            ReplayRunManifest.EnsureNoSensitiveInformation("{\"source\":\"Server=db;Password=secret\"}"));
    }

    [Fact]
    public void FalhaDeExportacao_NaoPermitePublicarManifestoComoCompleto()
    {
        var request = Request() with { ExportsCompleted = false, Artifacts = [] };

        var failure = Assert.Throws<ArgumentException>(() => ReplayRunManifest.Create(request));

        Assert.Contains("exportacao concluida", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NovaExportacaoInvalidaManifestoAntigoAntesDeQualquerNovoArtefato()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var previousManifest = Path.Combine(directory, "intelligence-lab-run-manifest.json");
        File.WriteAllText(previousManifest, "{\"old\":true}");

        try
        {
            var preparedPath = ReplayRunManifest.PrepareOutputManifestPath(directory);

            Assert.True(string.Equals(previousManifest, preparedPath,
                StringComparison.OrdinalIgnoreCase));
            Assert.False(File.Exists(preparedPath));
            // Simula falha durante a exportação posterior: não há manifesto
            // antigo que possa aparentar validar os arquivos da nova execução.
            Assert.False(File.Exists(previousManifest));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GitEObtidoDaRaizDaIAApiMesmoComDiretorioDeExecucaoDiferente()
    {
        var root = CreateIaApiRoot();
        var otherDirectory = Path.Combine(Path.GetTempPath(), $"other-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(otherDirectory);
        var calls = new List<string>();
        try
        {
            var metadata = ReplayGitMetadataResolver.Read(
                [otherDirectory, Path.Combine(root, "bin", "Debug", "net10.0")],
                (repositoryRoot, arguments) =>
                {
                    calls.Add(repositoryRoot);
                    Assert.True(string.Equals(root, repositoryRoot,
                        StringComparison.OrdinalIgnoreCase));
                    return arguments.SequenceEqual(["rev-parse", "--show-toplevel"])
                        ? root
                        : arguments.SequenceEqual(["rev-parse", "HEAD"])
                            ? "e58ab3d75e074ddfda3f569e00514606024a4831"
                            : string.Empty;
                });

            Assert.Equal("e58ab3d75e074ddfda3f569e00514606024a4831", metadata.Commit);
            Assert.Equal(false, metadata.HasUncommittedChanges);
            Assert.Equal(ManifestVerificationStatus.Verified, metadata.VerificationStatus);
            Assert.All(calls, value => Assert.True(string.Equals(root, value,
                StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(otherDirectory, recursive: true);
        }
    }

    [Fact]
    public void GitIndisponivelRegistraUnknownSemInventarCommit()
    {
        var root = CreateIaApiRoot();
        try
        {
            var metadata = ReplayGitMetadataResolver.Read([root], (_, _) => null);

            Assert.Null(metadata.Commit);
            Assert.Null(metadata.HasUncommittedChanges);
            Assert.Equal(ManifestVerificationStatus.Unknown, metadata.VerificationStatus);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ManifestoExcluiHashProprioERegistraHashesDosArtefatos()
    {
        var manifest = ReplayRunManifest.Create(Request());
        var json = ReplayRunManifest.Serialize(manifest);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("ManifestExcludedToAvoidCircularHash", manifest.SelfHashPolicy);
        Assert.Equal(2, manifest.Artifacts.Count);
        Assert.False(document.RootElement.TryGetProperty("sha256", out _));
    }

    private static ReplayRunManifestRequest Request(
        ReplayManifestGit? git = null,
        ReplayManifestConfiguration? configuration = null)
    {
        var inputBytes = Encoding.UTF8.GetBytes("input");
        var sha = ReplayRunManifest.Sha256Hex(inputBytes);
        var input = new ReplayManifestInput("input.json", sha, inputBytes.Length,
            Start, Start.AddSeconds(1), ManifestVerificationStatus.Verified);
        var settings = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["ensembleThreshold"] = 60d,
            ["runtimeFunctionalDecisionEnabled"] = false
        };
        configuration ??= new ReplayManifestConfiguration("OfflineIsolatedHistoricalReplay",
            ["Statistical"], ["IsolationForest"], settings,
            ReplayRunManifest.CreateConfigurationFingerprint("OfflineIsolatedHistoricalReplay",
                ["Statistical"], ["IsolationForest"], settings), ManifestVerificationStatus.Verified);
        var artifact = new ReplayManifestArtifact("export.json", sha, inputBytes.Length,
            ManifestVerificationStatus.Verified);
        return new ReplayRunManifestRequest("replay-unlabeled-abc123", Start,
            Start.AddMinutes(1), input, input,
            git ?? new ReplayManifestGit("e58ab3d", false, ManifestVerificationStatus.Verified,
                "Commit identified; local tree clean."),
            configuration,
            [new ReplayManifestContract("ReplayReadingsExport", "industrial-replay-readings-export/v1",
                ManifestVerificationStatus.Verified, "Produced")],
            [new ReplayManifestResource("StatisticalDetectors", ManifestResourceExecutionStatus.Executed,
                ManifestVerificationStatus.Verified, "Instantiated in process.")],
            [artifact, artifact with { Name = "report.json" }],
            ExportsCompleted: true);
    }

    private static string CreateIaApiRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"iaapi-root-{Guid.NewGuid():N}");
        var toolDirectory = Path.Combine(root, "tools", "IntelligenceLabReplay");
        Directory.CreateDirectory(toolDirectory);
        File.WriteAllText(Path.Combine(root, "IAApi.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(toolDirectory, "IAApi.IntelligenceLabReplay.csproj"), "<Project />");
        return root;
    }
}
