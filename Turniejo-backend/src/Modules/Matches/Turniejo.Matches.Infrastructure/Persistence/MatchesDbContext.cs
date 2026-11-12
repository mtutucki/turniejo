using Microsoft.EntityFrameworkCore;
using Npgsql;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.Infrastructure.Persistence;

public sealed class MatchesDbContext(DbContextOptions<MatchesDbContext> options)
    : DbContext(options), IMatchesDbContext
{
    public DbSet<Pitch> Pitches => Set<Pitch>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await OutboxMessages.AddAsync(message, cancellationToken);
    }

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "IX_matches_PitchId_ScheduledAt" })
        {
            // Aplikacyjny check-then-save (MatchExistsAtPitchAndTimeAsync) to nie wystarczy przy
            // równoległych żądaniach — to jest ostatnia linia obrony, więc zamieniamy surowy błąd bazy
            // na czytelny DomainException (422), a nie 500.
            throw new DomainException("Another match is already scheduled on this pitch at this time.");
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddPitchAsync(Pitch pitch, CancellationToken cancellationToken = default) =>
        await Pitches.AddAsync(pitch, cancellationToken);

    public async Task<Pitch?> GetPitchByIdAsync(Guid pitchId, CancellationToken cancellationToken = default) =>
        await Pitches.FirstOrDefaultAsync(p => p.Id == pitchId, cancellationToken);

    public async Task<IReadOnlyList<Pitch>> GetPitchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await Pitches
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Pitch>> GetPitchesByIdsAsync(IReadOnlyCollection<Guid> pitchIds, CancellationToken cancellationToken = default) =>
        await Pitches
            .AsNoTracking()
            .Where(p => pitchIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Pitch>> GetPitchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await Pitches
            .Where(p => p.GroupIds.Contains(groupId))
            .ToListAsync(cancellationToken);

    public async Task<bool> PitchNameExistsAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default) =>
        await Pitches.AnyAsync(p => p.TournamentId == tournamentId && p.Name == name, cancellationToken);

    public Task RemovePitchAsync(Pitch pitch, CancellationToken cancellationToken = default)
    {
        Pitches.Remove(pitch);
        return Task.CompletedTask;
    }

    public async Task AddMatchAsync(Match match, CancellationToken cancellationToken = default) =>
        await Matches.AddAsync(match, cancellationToken);

    public async Task AddMatchesAsync(IReadOnlyCollection<Match> matches, CancellationToken cancellationToken = default) =>
        await Matches.AddRangeAsync(matches, cancellationToken);

    public Task RemoveMatchesAsync(IReadOnlyCollection<Match> matches, CancellationToken cancellationToken = default)
    {
        Matches.RemoveRange(matches);
        return Task.CompletedTask;
    }

    public async Task<Match?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        await Matches.FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountLiveMatchesByTournamentIdsAsync(IReadOnlyCollection<Guid> tournamentIds, CancellationToken cancellationToken = default) =>
        await Matches
            .AsNoTracking()
            .Where(m => tournamentIds.Contains(m.TournamentId) && (m.Status == MatchStatus.Live || m.Status == MatchStatus.Paused))
            .GroupBy(m => m.TournamentId)
            .Select(g => new { TournamentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TournamentId, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<Match>> GetMatchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await Matches
            .AsNoTracking()
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Match>> GetFinishedMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await Matches
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && m.Status == MatchStatus.Finished)
            .ToListAsync(cancellationToken);

    public async Task<bool> MatchExistsAtPitchAndTimeAsync(Guid pitchId, DateTimeOffset scheduledAt, Guid? excludeMatchId, CancellationToken cancellationToken = default) =>
        await Matches.AnyAsync(m =>
            m.PitchId == pitchId && m.ScheduledAt == scheduledAt && m.Id != excludeMatchId,
            cancellationToken);

    public async Task<IReadOnlyList<Match>> GetMatchesByIdsAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default) =>
        await Matches
            .AsNoTracking()
            .Where(m => matchIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Match>> GetTrackedMatchesByIdsAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default) =>
        await Matches
            .Where(m => matchIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Match>> GetTrackedUnfinishedMatchesStartedBeforeAsync(DateTimeOffset startedBefore, CancellationToken cancellationToken = default) =>
        await Matches
            .Where(m => (m.Status == MatchStatus.Live || m.Status == MatchStatus.Paused) && m.StartedAt < startedBefore && m.AwaitingDecisionSince == null)
            .ToListAsync(cancellationToken);

    public async Task<bool> AnyMatchesByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await Matches.AnyAsync(m => m.CategoryId == categoryId, cancellationToken);

    public async Task<bool> AnyMatchesByPitchIdAsync(Guid pitchId, CancellationToken cancellationToken = default) =>
        await Matches.AnyAsync(m => m.PitchId == pitchId, cancellationToken);

    public async Task<bool> AnyMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await Matches.AnyAsync(m => m.GroupId == groupId, cancellationToken);

    public async Task<bool> AnyUnfinishedMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await Matches.AnyAsync(
            m => m.GroupId == groupId && m.Status != MatchStatus.Finished && m.Status != MatchStatus.Cancelled,
            cancellationToken);

    private static readonly MatchStatus[] CancellableStatuses = [MatchStatus.Scheduled, MatchStatus.Ready, MatchStatus.Postponed];

    public async Task<IReadOnlyList<Match>> GetCancellableMatchesByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        await Matches
            .Where(m => (m.HomeTournamentTeamId == tournamentTeamId || m.AwayTournamentTeamId == tournamentTeamId)
                        && CancellableStatuses.Contains(m.Status))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Match>> GetCancellableMatchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await Matches
            .Where(m => m.TournamentId == tournamentId && CancellableStatuses.Contains(m.Status))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Match>> GetMatchesByTournamentTeamIdsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default) =>
        await Matches
            .AsNoTracking()
            .Where(m => tournamentTeamIds.Contains(m.HomeTournamentTeamId) || tournamentTeamIds.Contains(m.AwayTournamentTeamId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Match>> GetMatchesScheduledBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        await Matches
            .AsNoTracking()
            .Where(m => m.Status == MatchStatus.Scheduled && m.ScheduledAt >= from && m.ScheduledAt <= to)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLastEventChangeByMatchIdsAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default) =>
        await MatchEvents
            .AsNoTracking()
            .Where(e => matchIds.Contains(e.MatchId))
            .GroupBy(e => e.MatchId)
            .Select(g => new { MatchId = g.Key, LastChange = g.Max(e => e.UpdatedAt) })
            .ToDictionaryAsync(x => x.MatchId, x => x.LastChange, cancellationToken);

    public async Task AddMatchEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken = default) =>
        await MatchEvents.AddAsync(matchEvent, cancellationToken);

    public async Task<IReadOnlyList<MatchEvent>> GetMatchEventsByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        await MatchEvents
            .AsNoTracking()
            .Where(e => e.MatchId == matchId && !e.IsDeleted)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MatchEvent>> GetActiveMatchEventsByTypeAsync(Guid matchId, MatchEventType type, CancellationToken cancellationToken = default) =>
        await MatchEvents
            .Where(e => e.MatchId == matchId && e.Type == type && !e.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<MatchEvent?> GetMatchEventByIdAsync(Guid matchEventId, CancellationToken cancellationToken = default) =>
        await MatchEvents.FirstOrDefaultAsync(e => e.Id == matchEventId, cancellationToken);

    public async Task<IReadOnlyList<MatchEvent>> GetMatchEventsByTournamentIdAndTypesAsync(
        Guid tournamentId, IReadOnlyCollection<MatchEventType> types, CancellationToken cancellationToken = default) =>
        await (
            from e in MatchEvents.AsNoTracking()
            join m in Matches.AsNoTracking() on e.MatchId equals m.Id
            where m.TournamentId == tournamentId && !e.IsDeleted && types.Contains(e.Type)
            select e
        ).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MatchEvent>> GetMatchEventsByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        await MatchEvents
            .AsNoTracking()
            .Where(e => e.TournamentTeamId == tournamentTeamId && !e.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
        await AuditLogs.AddAsync(auditLog, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("matches");

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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MatchesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
