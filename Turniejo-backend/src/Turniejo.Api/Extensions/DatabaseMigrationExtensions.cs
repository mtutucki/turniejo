using Microsoft.EntityFrameworkCore;
using Turniejo.Brackets.Infrastructure.Persistence;
using Turniejo.Matches.Infrastructure.Persistence;
using Turniejo.Notifications.Infrastructure.Persistence;
using Turniejo.Organizations.Infrastructure.Persistence;
using Turniejo.Players.Infrastructure.Persistence;
using Turniejo.Standings.Infrastructure.Persistence;
using Turniejo.Teams.Infrastructure.Persistence;
using Turniejo.Tournaments.Infrastructure.Persistence;
using Turniejo.Users.Infrastructure.Persistence;

namespace Turniejo.Api.Extensions;

// 9 modułów = 9 DbContextów, każdy z własnym schematem i własną tabelą __EFMigrationsHistory.
// Jedna lista dla produkcji (Database:MigrateOnStartup) i testów integracyjnych — nowy moduł
// dopisany tylko w jednym z tych miejsc skończyłby się brakującymi tabelami w drugim.
public static class DatabaseMigrationExtensions
{
    public static async Task MigrateAllModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<OrganizationsDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<TournamentsDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<TeamsDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<PlayersDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<MatchesDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<StandingsDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<BracketsDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync(cancellationToken);
    }

    // Wołane po Build(), a przed Run() — hosted services (OutboxProcessor, MatchReminder) startują
    // dopiero w Run(), więc nie trafią na nieistniejące tabele. Jedna instancja API na VPS, więc
    // nie ma wyścigu dwóch procesów migrujących naraz.
    public static async Task MigrateDatabaseIfEnabledAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
            return;

        using var scope = app.Services.CreateScope();
        app.Logger.LogInformation("Applying database migrations for all modules");
        await scope.ServiceProvider.MigrateAllModulesAsync();
    }
}
