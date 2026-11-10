using Microsoft.EntityFrameworkCore;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Infrastructure.Persistence;

public sealed class TeamsDbContext(DbContextOptions<TeamsDbContext> options)
    : DbContext(options), ITeamsDbContext
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<TeamStaff> TeamStaffs => Set<TeamStaff>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await OutboxMessages.AddAsync(message, cancellationToken);
    }

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTeamAsync(Team team, CancellationToken cancellationToken = default) =>
        await Teams.AddAsync(team, cancellationToken);

    public async Task<Team?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

    public async Task<IReadOnlyList<Team>> GetTeamsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await Teams
            .AsNoTracking()
            .Where(t => t.OrganizationId == organizationId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Team>> GetTeamsByIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0)
            return [];

        return await Teams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetTournamentTeamIdsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await TournamentTeams
            .Where(tt => Teams.Any(t => t.Id == tt.TeamId && t.OrganizationId == organizationId))
            .Select(tt => tt.Id)
            .ToListAsync(cancellationToken);

    // ExecuteDelete — od razu w bazie, bez SaveChanges (usunięcie organizacji woła to z innego modułu).
    // Sztab znika kaskadą FK; zgłoszenia trzeba usunąć przed drużynami (FK Restrict), więc w transakcji.
    public async Task RemoveTeamsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await TournamentTeams
            .Where(tt => Teams.Any(t => t.Id == tt.TeamId && t.OrganizationId == organizationId))
            .ExecuteDeleteAsync(cancellationToken);
        await Teams
            .Where(t => t.OrganizationId == organizationId)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddTournamentTeamAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default) =>
        await TournamentTeams.AddAsync(tournamentTeam, cancellationToken);

    public async Task<TournamentTeam?> GetTournamentTeamByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.FirstOrDefaultAsync(t => t.Id == tournamentTeamId, cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> GetTournamentTeamsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await TournamentTeams
            .AsNoTracking()
            .Where(t => t.TournamentId == tournamentId)
            // Bez sortowania Postgres przestawia wiersz po każdej zmianie (np. akceptacji),
            // a drużyny o tej samej nazwie zamieniały się miejscami na liście.
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountAcceptedTeamsByTournamentIdsAsync(IReadOnlyCollection<Guid> tournamentIds, CancellationToken cancellationToken = default) =>
        await TournamentTeams
            .AsNoTracking()
            .Where(t => tournamentIds.Contains(t.TournamentId) && t.RegistrationStatus == RegistrationStatus.Accepted)
            .GroupBy(t => t.TournamentId)
            .Select(g => new { TournamentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TournamentId, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> GetTournamentTeamsByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await TournamentTeams
            .AsNoTracking()
            .Where(t => t.TeamId == teamId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> GetUngroupedTournamentTeamsForUpdateAsync(Guid tournamentId, Guid categoryId, CancellationToken cancellationToken = default) =>
        await TournamentTeams
            .Where(t => t.TournamentId == tournamentId && t.CategoryId == categoryId && t.GroupId == null)
            .ToListAsync(cancellationToken);

    public async Task<TournamentTeam?> GetTournamentTeamForUpdateAsync(Guid tournamentId, Guid teamId, Guid categoryId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.FirstOrDefaultAsync(t =>
            t.TournamentId == tournamentId && t.TeamId == teamId && t.CategoryId == categoryId,
            cancellationToken);

    public async Task<bool> TournamentTeamExistsAsync(Guid tournamentId, Guid teamId, Guid categoryId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.AnyAsync(t =>
            t.TournamentId == tournamentId && t.TeamId == teamId && t.CategoryId == categoryId,
            cancellationToken);

    public async Task<bool> AnyActiveTournamentTeamsByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.AnyAsync(t =>
            t.CategoryId == categoryId &&
            (t.RegistrationStatus == RegistrationStatus.Pending || t.RegistrationStatus == RegistrationStatus.Accepted),
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetInactiveTournamentTeamIdsByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await TournamentTeams
            .Where(t => t.CategoryId == categoryId &&
                (t.RegistrationStatus == RegistrationStatus.Rejected || t.RegistrationStatus == RegistrationStatus.Withdrawn))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

    // ExecuteDelete — od razu w bazie, bez SaveChanges (woła to moduł Tournaments przy usuwaniu kategorii).
    public async Task RemoveTournamentTeamsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default)
    {
        if (tournamentTeamIds.Count == 0)
            return;

        await TournamentTeams
            .Where(t => tournamentTeamIds.Contains(t.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> AnyActiveTournamentTeamsByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.AnyAsync(t =>
            t.GroupId == groupId &&
            (t.RegistrationStatus == RegistrationStatus.Pending || t.RegistrationStatus == RegistrationStatus.Accepted),
            cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> GetTournamentTeamsByGroupIdForUpdateAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.Where(t => t.GroupId == groupId).ToListAsync(cancellationToken);

    public async Task<int> CountAcceptedTournamentTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.CountAsync(t =>
            t.TournamentId == tournamentId && t.RegistrationStatus == RegistrationStatus.Accepted,
            cancellationToken);

    public async Task AddTeamStaffAsync(TeamStaff teamStaff, CancellationToken cancellationToken = default) =>
        await TeamStaffs.AddAsync(teamStaff, cancellationToken);

    public Task RemoveTeamStaffAsync(TeamStaff teamStaff, CancellationToken cancellationToken = default)
    {
        TeamStaffs.Remove(teamStaff);
        return Task.CompletedTask;
    }

    public async Task<bool> TeamStaffExistsAsync(Guid tournamentTeamId, Guid userId, TeamStaffRole role, CancellationToken cancellationToken = default) =>
        await TeamStaffs.AnyAsync(s =>
            s.TournamentTeamId == tournamentTeamId && s.UserId == userId && s.Role == role,
            cancellationToken);

    public async Task<IReadOnlyList<TeamStaff>> GetTeamStaffByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        await TeamStaffs
            .AsNoTracking()
            .Where(s => s.TournamentTeamId == tournamentTeamId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TeamStaff>> GetTeamStaffByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await TeamStaffs
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("teams");

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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeamsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
