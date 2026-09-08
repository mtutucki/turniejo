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

    public async Task AddTournamentTeamAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default) =>
        await TournamentTeams.AddAsync(tournamentTeam, cancellationToken);

    public async Task<TournamentTeam?> GetTournamentTeamByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.FirstOrDefaultAsync(t => t.Id == tournamentTeamId, cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> GetTournamentTeamsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await TournamentTeams
            .AsNoTracking()
            .Where(t => t.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

    public async Task<bool> TournamentTeamExistsAsync(Guid tournamentId, Guid teamId, Guid categoryId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.AnyAsync(t =>
            t.TournamentId == tournamentId && t.TeamId == teamId && t.CategoryId == categoryId,
            cancellationToken);

    public async Task<int> CountAcceptedTournamentTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await TournamentTeams.CountAsync(t =>
            t.TournamentId == tournamentId && t.RegistrationStatus == RegistrationStatus.Accepted,
            cancellationToken);

    public async Task AddTeamStaffAsync(TeamStaff teamStaff, CancellationToken cancellationToken = default) =>
        await TeamStaffs.AddAsync(teamStaff, cancellationToken);

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
