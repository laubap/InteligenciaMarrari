using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class BehaviorProfilesDependencyInjectionTests
{
    [Fact]
    public void AddBehaviorMemoryDiscovery_DeveResolverBehaviorRuntimeBaselineProvider()
    {
        var services =
            new ServiceCollection();

        services.AddDbContext<AppDbContext>(
            options =>
                options.UseSqlite(
                    "Data Source=:memory:"));

        services.AddBehaviorMemoryDiscovery();

        using var serviceProvider =
            services.BuildServiceProvider(
                validateScopes:
                    true);

        using var scope =
            serviceProvider.CreateScope();

        var runtimeProvider =
            scope.ServiceProvider
                .GetService<
                    BehaviorRuntimeBaselineProvider>();

        Assert.NotNull(
            runtimeProvider);
    }
}
