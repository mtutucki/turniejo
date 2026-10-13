using Microsoft.EntityFrameworkCore;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Players.Infrastructure.Persistence;

public sealed class PlayersDbContext(DbContextOptions<PlayersDbContext> options)
    : DbContext(options), IPlayersDbContext
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<TournamentPlayer> TournamentPlayers => Set<TournamentPlayer>();
    public DbSet<PlayerGuardian> PlayerGuardians => Set<PlayerGuardian>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await OutboxMessages.AddAsync(message, cancellationToken);
    }

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task AddPlayerAsync(Player player, CancellationToken cancellationToken = default) =>
        await Players.AddAsync(player, cancellationToken);

    public async Task<Player?> GetPlayerByIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        await Players.FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetPlayersByIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default) =>
        await Players
            .AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

    public async Task AddTournamentPlayerAsync(TournamentPlayer tournamentPlayer, CancellationToken cancellationToken = default) =>
        await TournamentPlayers.AddAsync(tournamentPlayer, cancellationToken);

    public async Task<TournamentPlayer?> GetTournamentPlayerByIdAsync(Guid tournamentPlayerId, CancellationToken cancellationToken = default) =>
        await TournamentPlayers.FirstOrDefaultAsync(p => p.Id == tournamentPlayerId, cancellationToken);

    public async Task<IReadOnlyList<TournamentPlayer>> GetTournamentPlayersByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        await TournamentPlayers
            .AsNoTracking()
            .Where(p => p.TournamentTeamId == tournamentTeamId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TournamentPlayer>> GetTournamentPlayersByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        await TournamentPlayers
            .AsNoTracking()
            .Where(p => p.PlayerId == playerId)
            .ToListAsync(cancellationToken);

    public async Task<bool> TournamentPlayerExistsAsync(Guid tournamentTeamId, Guid playerId, CancellationToken cancellationToken = default) =>
        await TournamentPlayers.AnyAsync(p =>
            p.TournamentTeamId == tournamentTeamId && p.PlayerId == playerId,
            cancellationToken);

    public async Task<bool> ShirtNumberExistsAsync(Guid tournamentTeamId, int shirtNumber, CancellationToken cancellationToken = default) =>
        await TournamentPlayers.AnyAsync(p =>
            p.TournamentTeamId == tournamentTeamId && p.ShirtNumber == shirtNumber,
            cancellationToken);

    public async Task AddPlayerGuardianAsync(PlayerGuardian playerGuardian, CancellationToken cancellationToken = default) =>
        await PlayerGuardians.AddAsync(playerGuardian, cancellationToken);

    public async Task<bool> PlayerGuardianExistsAsync(Guid playerId, Guid userId, CancellationToken cancellationToken = default) =>
        await PlayerGuardians.AnyAsync(g => g.PlayerId == playerId && g.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<PlayerGuardian>> GetPlayerGuardiansByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        await PlayerGuardians
            .AsNoTracking()
            .Where(g => g.PlayerId == playerId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerGuardian>> GetPlayerGuardiansByPlayerIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
            return [];

        return await PlayerGuardians
            .AsNoTracking()
            .Where(g => playerIds.Contains(g.PlayerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerGuardian>> GetGuardianshipsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await PlayerGuardians
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .ToListAsync(cancellationToken);

    // ExecuteDelete — od razu w bazie, bez SaveChanges (usunięcie konta woła to z innego modułu).
    public async Task RemoveGuardianshipsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await PlayerGuardians
            .Where(g => g.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    // ExecuteDelete — od razu w bazie, bez SaveChanges (usunięcie organizacji woła to z innego modułu).
    // W transakcji: po usunięciu samych składów nie dałoby się już ustalić, których zawodników dotyczyły.
    public async Task<IReadOnlyList<Guid>> RemovePlayersOfTournamentTeamsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default)
    {
        if (tournamentTeamIds.Count == 0)
            return [];

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var rosterPlayerIds = await TournamentPlayers
            .Where(tp => tournamentTeamIds.Contains(tp.TournamentTeamId))
            .Select(tp => tp.PlayerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await TournamentPlayers
            .Where(tp => tournamentTeamIds.Contains(tp.TournamentTeamId))
            .ExecuteDeleteAsync(cancellationToken);

        // Zawodnik grający także w drużynie innej organizacji zostaje — traci tylko te składy.
        var orphanedPlayerIds = await Players
            .Where(p => rosterPlayerIds.Contains(p.Id) && !TournamentPlayers.Any(tp => tp.PlayerId == p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        await Players
            .Where(p => orphanedPlayerIds.Contains(p.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return orphanedPlayerIds;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("players");

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.FindPrimaryKey()?.Properties
                .Any(p => p.ClrType == typeof(Guid)) == true)
            {
                entity.FindPrimaryKey()!.Properties
                    .Where(p => p.ClrType == typeof(Guid))
                    .ToList()
                    .ForEach(p => p.ValueGenerated =
                        Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never);
            }
        }

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlayersDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
