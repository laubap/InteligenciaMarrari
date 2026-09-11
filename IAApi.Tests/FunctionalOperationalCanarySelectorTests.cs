using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using Xunit;

namespace IAApi.Tests;

public sealed class FunctionalOperationalCanarySelectorTests
{
    private readonly FunctionalOperationalCanarySelector _selector = new();

    [Fact]
    public void CanaryDesabilitado_NaoRestringeRollout()
    {
        var options = new FunctionalOperationalCanaryOptions
        {
            Enabled = false
        };

        Assert.True(
            _selector.IsInCanaryScope(
                options,
                fastWindow: 99,
                tag: "ANY.TAG"));
    }

    [Fact]
    public void CanaryLigadoSemTags_FailClosed()
    {
        var options = new FunctionalOperationalCanaryOptions
        {
            Enabled = true,
            AllowedFastWindows = new HashSet<int> { 1 }
        };

        Assert.False(
            _selector.IsInCanaryScope(
                options,
                fastWindow: 1,
                tag: "TEST.TAG"));
    }

    [Fact]
    public void CanaryLigadoSemJanelas_FailClosed()
    {
        var options = new FunctionalOperationalCanaryOptions
        {
            Enabled = true,
            AllowedTags = new HashSet<string>(
                new[] { "TEST.TAG" },
                StringComparer.OrdinalIgnoreCase)
        };

        Assert.False(
            _selector.IsInCanaryScope(
                options,
                fastWindow: 1,
                tag: "TEST.TAG"));
    }

    [Fact]
    public void TagEJanelaPermitidas_EntramNoCanario()
    {
        var options = new FunctionalOperationalCanaryOptions
        {
            Enabled = true,
            AllowedTags = new HashSet<string>(
                new[] { "TEST.TAG" },
                StringComparer.OrdinalIgnoreCase),
            AllowedFastWindows = new HashSet<int> { 1 }
        };

        Assert.True(
            _selector.IsInCanaryScope(
                options,
                fastWindow: 1,
                tag: "test.tag"));
    }

    [Fact]
    public void TagForaDoEscopo_NaoEntraNoCanario()
    {
        var options = new FunctionalOperationalCanaryOptions
        {
            Enabled = true,
            AllowedTags = new HashSet<string>(
                new[] { "TEST.TAG" },
                StringComparer.OrdinalIgnoreCase),
            AllowedFastWindows = new HashSet<int> { 1 }
        };

        Assert.False(
            _selector.IsInCanaryScope(
                options,
                fastWindow: 1,
                tag: "OTHER.TAG"));
    }

    [Fact]
    public void JanelaForaDoEscopo_NaoEntraNoCanario()
    {
        var options = new FunctionalOperationalCanaryOptions
        {
            Enabled = true,
            AllowedTags = new HashSet<string>(
                new[] { "TEST.TAG" },
                StringComparer.OrdinalIgnoreCase),
            AllowedFastWindows = new HashSet<int> { 1 }
        };

        Assert.False(
            _selector.IsInCanaryScope(
                options,
                fastWindow: 2,
                tag: "TEST.TAG"));
    }
}