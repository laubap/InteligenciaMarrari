using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeRolloutSnapshotPersistenceOptionsTests
{
    [Fact]
    public void Defaults_SaoFailSafe()
    {
        var options =
            new RuntimeRolloutSnapshotPersistenceOptions();

        Assert.False(
            options.Enabled);

        Assert.Equal(
            5,
            options.IntervalMinutes);
    }
}