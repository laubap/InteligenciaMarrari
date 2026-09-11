
using Xunit;

namespace IAApi.Tests;

public sealed class MultivariateOperationalReadinessEvaluatorTests
{
    private readonly MultivariateOperationalReadinessEvaluator
        _evaluator = new();

    private static readonly DateTime NowUtc =
        new(
            2026, 9, 4, 18, 0, 0,
            DateTimeKind.Utc);

    [Fact]
    public void GrupoAprovadoComModeloCompativel_DeveEstarReady()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(),
                    Model(
                        trainedAtUtc:
                            NowUtc.AddDays(-1),
                        sampleCount:
                            1403)),
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]));

        Assert.True(
            result.ReadyForOperationalAuthority);

        Assert.True(
            result.ExplicitlyApproved);

        Assert.True(
            result.ModelPresent);

        Assert.Empty(
            result.Blockers);
    }

    [Fact]
    public void GrupoNaoAprovado_NaoPodeGanharAutoridade()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(),
                    Model()),
                Options(
                    approved: []));

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "GrupoNaoAprovadoParaAutoridadeOperacional",
            result.Blockers);
    }

    [Fact]
    public void ModeloAusente_NaoPodeGanharAutoridade()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(),
                    model: null),
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]));

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "ModeloAusente",
            result.Blockers);
    }

    [Fact]
    public void PoucasAmostras_NaoPodeGanharAutoridade()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(),
                    Model(
                        sampleCount:
                            99)),
                Options(
                    approved:
                        ["PK7::GRUPO-AB"],
                    minimumSamples:
                        100));

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "AmostrasTreinamentoInsuficientes",
            result.Blockers);
    }

    [Fact]
    public void ModeloObsoleto_NaoPodeGanharAutoridade()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(),
                    Model(
                        trainedAtUtc:
                            NowUtc.AddDays(-31))),
                Options(
                    approved:
                        ["PK7::GRUPO-AB"],
                    maxAgeDays:
                        30));

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "ModeloObsoleto",
            result.Blockers);
    }

    [Fact]
    public void ConfiguracaoDeTagsMudou_NaoPodeReutilizarModeloAntigo()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(
                        "TAG-A",
                        "TAG-C"),
                    Model(
                        tags:
                            ["TAG-A", "TAG-B"])),
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]));

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "TagsModeloDiferemDaConfiguracaoAtual",
            result.Blockers);
    }

    [Fact]
    public void CorrelacaoLinearFraca_DeveGerarWarningMasNaoSerRegraUnica()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(),
                    Model(
                        correlation:
                            0.0017,
                        sampleCount:
                            1403)),
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]));

        Assert.True(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "RelacaoLinearHistoricaFraca",
            result.Warnings);

        Assert.NotNull(
            result.MaximumAbsoluteLinearCorrelation);

        Assert.Equal(
            0.0017,
            result.MaximumAbsoluteLinearCorrelation!.Value,
            4);
    }

    [Fact]
    public void GrupoInativo_NaoPodeGanharAutoridade()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    Group(),
                    Model(),
                    groupIsActive:
                        false),
                Options(
                    approved:
                        ["PK7::GRUPO-AB"]));

        Assert.False(
            result.ReadyForOperationalAuthority);

        Assert.Contains(
            "GrupoInativo",
            result.Blockers);
    }

    private static MultivariateOperationalReadinessContext
        Context(
            GrupoAnaliseDto group,
            MultivariateOperationalModelSnapshot? model,
            bool groupIsActive = true)
    {
        return new MultivariateOperationalReadinessContext
        {
            ClienteId =
                "PK7",

            GroupIsActive =
                groupIsActive,

            Group =
                group,

            Model =
                model,

            NowUtc =
                NowUtc
        };
    }

    private static MultivariateOperationalReadinessOptions
        Options(
            string[] approved,
            int minimumSamples = 100,
            int maxAgeDays = 30)
    {
        return new MultivariateOperationalReadinessOptions
        {
            RequireExplicitApproval =
                true,

            ApprovedGroups =
                approved,

            MinimumTrainingSamples =
                minimumSamples,

            MaxModelAgeDays =
                maxAgeDays,

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

    private static MultivariateOperationalModelSnapshot Model(
        long sampleCount = 1403,
        DateTime? trainedAtUtc = null,
        string[]? tags = null,
        double correlation = 0.85)
    {
        var modelTags =
            tags ??
            ["TAG-A", "TAG-B"];

        return new MultivariateOperationalModelSnapshot
        {
            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            TrainedAtUtc =
                trainedAtUtc ??
                NowUtc.AddDays(-1),

            SampleCount =
                sampleCount,

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
                [1, correlation],
                [correlation, 1]
            ],

            CorrelationMatrix =
            [
                [1, correlation],
                [correlation, 1]
            ]
        };
    }
}
