using System.Text.Json;
using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

public sealed class MultivariateOperationalReadinessServiceTests
{
    private static readonly DateTime NowUtc =
        new(
            2026, 9, 4, 18, 0, 0,
            DateTimeKind.Utc);

    [Fact]
    public async Task GrupoAtivoComModeloValidoEAprovado_DeveEstarReady()
    {
        using var fixture =
            Fixture.Create(
                Group());

        await fixture.GravarModeloAsync(
            Model());

        var result =
            await fixture.Service.AvaliarAsync(
                "PK7",
                "GRUPO-AB",
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]),
                NowUtc);

        Assert.True(
            result.ReadyForOperationalAuthority);

        Assert.True(
            result.ExplicitlyApproved);

        Assert.True(
            result.ModelPresent);

        Assert.Equal(
            1403,
            result.ModelSampleCount);

        Assert.Empty(
            result.Blockers);
    }

    [Fact]
    public async Task GrupoAtivoSemModelo_DeveFalharFechado()
    {
        using var fixture =
            Fixture.Create(
                Group());

        var result =
            await fixture.Service.AvaliarAsync(
                "PK7",
                "GRUPO-AB",
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]),
                NowUtc);

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "ModeloAusente",
            result.Blockers);
    }

    [Fact]
    public async Task GrupoNaoExpostoPeloProviderOperacional_DeveSerAusente()
    {
        using var fixture =
            Fixture.Create();

        var result =
            await fixture.Service.AvaliarAsync(
                "PK7",
                "GRUPO-INEXISTENTE",
                Options(
                    approved:
                        ["PK7::GRUPO-INEXISTENTE"]),
                NowUtc);

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "GrupoAusente",
            result.Blockers);
    }

    [Fact]
    public async Task ModeloComTagsDiferentesDaConfiguracao_DeveSerBloqueado()
    {
        using var fixture =
            Fixture.Create(
                Group(
                    "TAG-A",
                    "TAG-B"));

        await fixture.GravarModeloAsync(
            Model(
                tags:
                    ["TAG-A", "TAG-C"]));

        var result =
            await fixture.Service.AvaliarAsync(
                "PK7",
                "GRUPO-AB",
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]),
                NowUtc);

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "TagsModeloDiferemDaConfiguracaoAtual",
            result.Blockers);
    }

    [Fact]
    public async Task FalhaDoProvider_DeveFalharFechadoSemDarAutoridade()
    {
        using var fixture =
            Fixture.CreateWithProvider(
                new ThrowingGrupoProvider());

        var result =
            await fixture.Service.AvaliarAsync(
                "PK7",
                "GRUPO-AB",
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]),
                NowUtc);

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "FalhaAoCarregarConfiguracaoDoGrupo",
            result.Blockers);
    }

    [Fact]
    public async Task ModeloCorrompido_DeveFalharFechadoSemDarAutoridade()
    {
        using var fixture =
            Fixture.Create(
                Group());

        var caminho =
            fixture.Persistence
                .ObterCaminhoModelo(
                    "PK7",
                    "GRUPO-AB");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                caminho)!);

        await File.WriteAllTextAsync(
            caminho,
            "{ json-invalido ");

        fixture.Persistence.InvalidarCache(
            "PK7",
            "GRUPO-AB");

        var result =
            await fixture.Service.AvaliarAsync(
                "PK7",
                "GRUPO-AB",
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]),
                NowUtc);

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "FalhaAoCarregarModelo",
            result.Blockers);

        Assert.True(
            result.ModelPresent);
    }

    private static MultivariateOperationalReadinessOptions
        Options(
            string[] approved)
    {
        return new MultivariateOperationalReadinessOptions
        {
            RequireExplicitApproval =
                true,

            MinimumTrainingSamples =
                100,

            MaxModelAgeDays =
                30,

            ApprovedGroups =
                approved,

            WeakLinearRelationshipWarningBelow =
                0.30
        };
    }

    private static GrupoAnaliseDto Group(
        params string[] tags)
    {
        return new GrupoAnaliseDto
        {
            Id =
                1,

            Nome =
                "GRUPO-AB",

            MinimumAvailableTags =
                2,

            MaxLagMinutes =
                2,

            Tags =
                tags.Length == 0
                    ? ["TAG-A", "TAG-B"]
                    : tags.ToList()
        };
    }

    private static MultivariateGroupModel Model(
        string[]? tags = null)
    {
        var modelTags =
            tags ??
            ["TAG-A", "TAG-B"];

        return new MultivariateGroupModel
        {
            Version =
                1,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            TrainedAtUtc =
                NowUtc.AddDays(-1),

            Algorithm =
                "OnlineMeanCovariance",

            SampleCount =
                1403,

            MinimumAvailableTags =
                2,

            MaxLagMinutes =
                2,

            Tags =
                modelTags.ToList(),

            Means =
                [10, 20],

            StandardDeviations =
                [1, 1],

            CovarianceMatrix =
            [
                [1, 0.85],
                [0.85, 1]
            ],

            CorrelationMatrix =
            [
                [1, 0.85],
                [0.85, 1]
            ]
        };
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _tempRoot;

        private Fixture(
            string tempRoot,
            IGrupoAnaliseProvider provider)
        {
            _tempRoot =
                tempRoot;

            Persistence =
                new MultivariateModelPersistenceService(
                    new TestWebHostEnvironment
                    {
                        ContentRootPath =
                            tempRoot
                    });

            Service =
                new MultivariateOperationalReadinessService(
                    provider,
                    Persistence,
                    new MultivariateOperationalReadinessEvaluator());
        }

        public MultivariateModelPersistenceService
            Persistence { get; }

        public MultivariateOperationalReadinessService
            Service { get; }

        public static Fixture Create(
            params GrupoAnaliseDto[] groups)
        {
            return CreateWithProvider(
                new StaticGrupoProvider(
                    groups));
        }

        public static Fixture CreateWithProvider(
            IGrupoAnaliseProvider provider)
        {
            var tempRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "IAApiTests",
                    "MV07_2",
                    Guid.NewGuid()
                        .ToString("N"));

            Directory.CreateDirectory(
                tempRoot);

            return new Fixture(
                tempRoot,
                provider);
        }

        public async Task GravarModeloAsync(
            MultivariateGroupModel model)
        {
            var caminho =
                Persistence.ObterCaminhoModelo(
                    model.ClienteId,
                    model.GroupName);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    caminho)!);

            await File.WriteAllTextAsync(
                caminho,
                JsonSerializer.Serialize(
                    model));

            Persistence.InvalidarCache(
                model.ClienteId,
                model.GroupName);
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    _tempRoot))
            {
                Directory.Delete(
                    _tempRoot,
                    recursive:
                        true);
            }
        }
    }

    private sealed class StaticGrupoProvider
        : IGrupoAnaliseProvider
    {
        private readonly List<GrupoAnaliseDto>
            _groups;

        public StaticGrupoProvider(
            params GrupoAnaliseDto[] groups)
        {
            _groups =
                groups.ToList();
        }

        public Task<List<GrupoAnaliseDto>>
            CarregarGruposAsync(
                string clienteId)
        {
            return Task.FromResult(
                _groups.ToList());
        }
    }

    private sealed class ThrowingGrupoProvider
        : IGrupoAnaliseProvider
    {
        public Task<List<GrupoAnaliseDto>>
            CarregarGruposAsync(
                string clienteId)
        {
            throw new InvalidOperationException(
                "Falha simulada no provider.");
        }
    }

    private sealed class TestWebHostEnvironment
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } =
            "IAApi.Tests";

        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();

        public string WebRootPath { get; set; } =
            "";

        public string EnvironmentName { get; set; } =
            "Development";

        public string ContentRootPath { get; set; } =
            "";

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
