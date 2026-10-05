using Microsoft.Extensions.Diagnostics.HealthChecks;
using Turniejo.Users.Infrastructure.Persistence;

namespace Turniejo.Api.Extensions;

// Wszystkie moduły siedzą w jednej bazie (osobne schematy), więc jedno połączenie wystarczy, żeby
// /health odróżniał "proces żyje" od "proces żyje, ale Postgres leży" — to drugie też ma
// wywołać alert w monitoringu uptime.
internal sealed class DatabaseHealthCheck(UsersDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
