using System.Text.Json;
using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

public sealed class OperationalMultivariateBacktestServiceTests
{
    [Fact]
    public async Task HistoricoCompleto_DeveGerarResumoComTresVetores()
    {
        using var fixture =
            await Fixture.CreateAsync();

        fixture.History.Set(
            "TAG-A",
            Reading(10, 0),
            Reading(10, 1),
            Reading(15, 2));

        fixture.History.Set(
            "TAG-B",
            Reading(20, 0),
            Reading(20, 1),
            Reading(25, 2));

        var result =
            await fixture.Service
                .ExecutarAsync(
                    "PK7",
                    "GRUPO-AB",
                    At(10, 0),
                    At(10, 3));

        Assert.Equal(
            3,
            result.VetoresFinalizados);

        Assert.Equal(
            3,
            result.VetoresCompletos);

        Assert.Equal(
            0,
            result.VetoresIncompletos);

        Assert.Equal(
            3,
            result.Summary.ApplicableResults);

        Assert.Equal(
            1,
            result.Summary.AnomalyCount);

        Assert.Equal(
            2,
            result.Summary.NormalCount);
    }

    [Fact]
    public async Task DuasExecucoes_NaoDevemCompartilharEstadoTemporal()
    {
        using var fixture =
            await Fixture.CreateAsync();

        fixture.History.Set(
            "TAG-A",
            Reading(10, 0),
            Reading(10, 1));

        fixture.History.Set(
            "TAG-B",
            Reading(20, 0),
            Reading(20, 1));

        var first =
            await fixture.Service
                .ExecutarAsync(
                    "PK7",
                    "GRUPO-AB",
                    At(10, 0),
                    At(10, 2));

        var second =
            await fixture.Service
                .ExecutarAsync(
                    "PK7",
                    "GRUPO-AB",
                    At(10, 0),
                    At(10, 2));

        Assert.Equal(
            2,
            first.Summary.ApplicableResults);

        Assert.Equal(
            first.Summary.ApplicableResults,
            second.Summary.ApplicableResults);

        Assert.Equal(
            first.VetoresFinalizados,
            second.VetoresFinalizados);
    }

    [Fact]
    public async Task MultiplasLeiturasNoMesmoMinuto_DeveUsarAUltima()
    {
        using var fixture =
            await Fixture.CreateAsync();

        fixture.History.Set(
            "TAG-A",
            new LeituraBruta
            {
                DataHora =
                    At(10, 0)
                        .AddSeconds(5),

                Valor =
                    10
            },
            new LeituraBruta
            {
                DataHora =
                    At(10, 0)
                        .AddSeconds(50),

                Valor =
                    15
            });

        fixture.History.Set(
            "TAG-B",
            Reading(25, 0));

        var result =
            await fixture.Service
                .ExecutarAsync(
                    "PK7",
                    "GRUPO-AB",
                    At(10, 0),
                    At(10, 1));

        Assert.Single(
            result.Summary.TopEvents);

        Assert.True(
            result.Summary.TopEvents[0]
                .IsAnomaly);
    }

    [Fact]
    public async Task GrupoAusente_DeveFalharAntesDoReplay()
    {
        using var fixture =
            await Fixture.CreateAsync();

        var exception =
            await Assert.ThrowsAsync<
                KeyNotFoundException>(
                () =>
                    fixture.Service
                        .ExecutarAsync(
                            "PK7",
                            "OUTRO-GRUPO",
                            At(10, 0),
                            At(10, 1)));

        Assert.Contains(
            "OUTRO-GRUPO",
            exception.Message);
    }

    [Fact]
    public async Task ModeloAusente_DeveFalharAntesDoReplay()
    {
        using var fixture =
            await Fixture.CreateAsync(
                writeModel:
                    false);

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    fixture.Service
                        .ExecutarAsync(
                            "PK7",
                            "GRUPO-AB",
                            At(10, 0),
                            At(10, 1)));

        Assert.Contains(
            "Nenhum modelo",
            exception.Message);
    }

    [Fact]
    public async Task TagSemHistorico_DeveFalharFechado()
    {
        using var fixture =
            await Fixture.CreateAsync();

        fixture.History.Set(
            "TAG-A",
            Reading(10, 0));

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    fixture.Service
                        .ExecutarAsync(
                            "PK7",
                            "GRUPO-AB",
                            At(10, 0),
                            At(10, 1)));

        Assert.Contains(
            "TAG-B",
            exception.Message);
    }

    private static DateTime At(
        int hour,
        int minute)
    {
        return new DateTime(
            2026,
            9,
            4,
            hour,
            minute,
            0,
            DateTimeKind.Unspecified);
    }

    private static LeituraBruta Reading(
        float value,
        int minuteOffset)
    {
        return new LeituraBruta
        {
            DataHora =
                At(10, minuteOffset),

            Valor =
                value
        };
    }

    private sealed class Fixture
        : IDisposable
    {
        private readonly string _tempRoot;

        private Fixture(
            string tempRoot,
            FakeHistoricoService history,
            OperationalMultivariateBacktestService service)
        {
            _tempRoot =
                tempRoot;

            History =
                history;

            Service =
                service;
        }

        public FakeHistoricoService History { get; }

        public OperationalMultivariateBacktestService Service { get; }

        public static async Task<Fixture> CreateAsync(
            bool writeModel = true)
        {
            var tempRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "IAApiTests",
                    "MV08_2B",
                    Guid.NewGuid()
                        .ToString("N"));

            Directory.CreateDirectory(
                tempRoot);

            var environment =
                new TestWebHostEnvironment
                {
                    ContentRootPath =
                        tempRoot
                };

            var persistence =
                new MultivariateModelPersistenceService(
                    environment);

            if (writeModel)
            {
                var path =
                    persistence
                        .ObterCaminhoModelo(
                            "PK7",
                            "GRUPO-AB");

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        path)!);

                await File.WriteAllTextAsync(
                    path,
                    JsonSerializer.Serialize(
                        new MultivariateGroupModel
                        {
                            Version =
                                1,

                            ClienteId =
                                "PK7",

                            GroupName =
                                "GRUPO-AB",

                            TrainedAtUtc =
                                new DateTime(
                                    2026,
                                    9,
                                    3,
                                    12,
                                    0,
                                    0,
                                    DateTimeKind.Utc),

                            Algorithm =
                                "OnlineMeanCovariance",

                            SampleCount =
                                1000,

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
                                [1, 0],
                                [0, 1]
                            ],

                            CorrelationMatrix =
                            [
                                [1, 0],
                                [0, 1]
                            ]
                        }));
            }

            var provider =
                new StaticGrupoProvider(
                    new GrupoAnaliseDto
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
                    });

            var history =
                new FakeHistoricoService();

            var analyzer =
                new MultivariateHistoricalBacktestAnalyzer();

            var service =
                new OperationalMultivariateBacktestService(
                    provider,
                    history,
                    persistence,
                    analyzer);

            return new Fixture(
                tempRoot,
                history,
                service);
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
                _groups
                    .Select(group =>
                        new GrupoAnaliseDto
                        {
                            Id =
                                group.Id,

                            Nome =
                                group.Nome,

                            MinimumAvailableTags =
                                group.MinimumAvailableTags,

                            MaxLagMinutes =
                                group.MaxLagMinutes,

                            Tags =
                                group.Tags
                                    .ToList()
                        })
                    .ToList());
        }
    }

    private sealed class FakeHistoricoService
        : IHistoricoService
    {
        private readonly Dictionary<
            string,
            List<LeituraBruta>> _history =
                new(
                    StringComparer.OrdinalIgnoreCase);

        public void Set(
            string tag,
            params LeituraBruta[] readings)
        {
            _history[tag] =
                readings.ToList();
        }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return BuscarHistoricoPorPeriodo(
                clienteId,
                tagName,
                DateTime.MinValue,
                DateTime.MaxValue);
        }

        public List<LeituraBruta>
            BuscarHistoricoPorPeriodo(
                string clienteId,
                string tagName,
                DateTime inicio,
                DateTime fim)
        {
            if (!_history.TryGetValue(
                    tagName,
                    out var readings))
            {
                return [];
            }

            return readings
                .Where(item =>
                    item.DataHora >= inicio &&
                    item.DataHora <= fim)
                .Select(item =>
                    new LeituraBruta
                    {
                        DataHora =
                            item.DataHora,

                        Valor =
                            item.Valor
                    })
                .ToList();
        }
    }

    private sealed class TestWebHostEnvironment
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } =
            "IAApi.Tests";

        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();

        public string? WebRootPath { get; set; }

        public string EnvironmentName { get; set; } =
            "Development";

        public string ContentRootPath { get; set; } =
            "";

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}