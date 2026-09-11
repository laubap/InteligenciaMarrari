using System.Text.Json;
using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class MultivariateOperationalReadinessControllerTests
{
    private static readonly DateTime TrainedAtUtc =
        new(
            2026, 9, 4, 12, 0, 0,
            DateTimeKind.Utc);

    [Fact]
    public async Task GrupoAprovadoComModeloValido_DeveRetornarOkReady()
    {
        using var fixture =
            Fixture.Create(
                Group());

        await fixture.GravarModeloAsync(
            Model());

        var controller =
            fixture.CriarController(
                new MultivariateOperationalReadinessOptions
                {
                    RequireExplicitApproval =
                        true,

                    MinimumTrainingSamples =
                        100,

                    MaxModelAgeDays =
                        30,

                    ApprovedGroups =
                        ["PK7::GRUPO-AB"]
                });

        var action =
            await controller.Obter(
                "PK7",
                "GRUPO-AB",
                CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                action.Result);

        var result =
            Assert.IsType<
                MultivariateOperationalReadinessResult>(
                ok.Value);

        Assert.True(
            result.ReadyForOperationalAuthority);

        Assert.True(
            result.ModelPresent);

        Assert.True(
            result.ExplicitlyApproved);

        Assert.Empty(
            result.Blockers);
    }

    [Fact]
    public async Task GrupoSemAprovacao_DeveRetornarOkComBlocker()
    {
        using var fixture =
            Fixture.Create(
                Group());

        await fixture.GravarModeloAsync(
            Model());

        var controller =
            fixture.CriarController(
                new MultivariateOperationalReadinessOptions
                {
                    RequireExplicitApproval =
                        true,

                    MinimumTrainingSamples =
                        100,

                    MaxModelAgeDays =
                        30,

                    ApprovedGroups =
                        []
                });

        var action =
            await controller.Obter(
                "PK7",
                "GRUPO-AB",
                CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                action.Result);

        var result =
            Assert.IsType<
                MultivariateOperationalReadinessResult>(
                ok.Value);

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "GrupoNaoAprovadoParaAutoridadeOperacional",
            result.Blockers);
    }

    [Fact]
    public async Task ClienteInvalido_DeveRetornarBadRequest()
    {
        using var fixture =
            Fixture.Create(
                Group());

        var controller =
            fixture.CriarController(
                new MultivariateOperationalReadinessOptions());

        var action =
            await controller.Obter(
                " ",
                "GRUPO-AB",
                CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            action.Result);
    }

    private static GrupoAnaliseDto Group()
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
                ["TAG-A", "TAG-B"]
        };
    }

    private static MultivariateGroupModel Model()
    {
        return new MultivariateGroupModel
        {
            Version =
                1,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            TrainedAtUtc =
                TrainedAtUtc,

            Algorithm =
                "OnlineMeanCovariance",

            SampleCount =
                1403,

            MinimumAvailableTags =
                2,

            MaxLagMinutes =
                2,

            Tags =
                ["TAG-A", "TAG-B"],

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

        private readonly MultivariateModelPersistenceService
            _persistence;

        private readonly MultivariateOperationalReadinessService
            _service;

        private Fixture(
            string tempRoot,
            IGrupoAnaliseProvider provider)
        {
            _tempRoot =
                tempRoot;

            _persistence =
                new MultivariateModelPersistenceService(
                    new TestWebHostEnvironment
                    {
                        ContentRootPath =
                            tempRoot
                    });

            _service =
                new MultivariateOperationalReadinessService(
                    provider,
                    _persistence,
                    new MultivariateOperationalReadinessEvaluator());
        }

        public static Fixture Create(
            params GrupoAnaliseDto[] groups)
        {
            var tempRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "IAApiTests",
                    "MV07_3",
                    Guid.NewGuid()
                        .ToString("N"));

            Directory.CreateDirectory(
                tempRoot);

            return new Fixture(
                tempRoot,
                new StaticGrupoProvider(
                    groups));
        }

        public MultivariateOperationalReadinessController
            CriarController(
                MultivariateOperationalReadinessOptions options)
        {
            return new MultivariateOperationalReadinessController(
                _service,
                Options.Create(
                    options));
        }

        public async Task GravarModeloAsync(
            MultivariateGroupModel model)
        {
            var caminho =
                _persistence.ObterCaminhoModelo(
                    model.ClienteId,
                    model.GroupName);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    caminho)!);

            await File.WriteAllTextAsync(
                caminho,
                JsonSerializer.Serialize(
                    model));

            _persistence.InvalidarCache(
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
