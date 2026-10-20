using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.Runtime.Persistence;

public interface IRuntimeRolloutDbContext
{
    DbSet<RuntimeRolloutSnapshot> RuntimeRolloutSnapshots { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}