using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.Abstractions
{
    public interface ITeamsDbContext
    {
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Team
        Task AddTeamAsync(Team team, CancellationToken cancellationToken = default);
        Task<Team?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Team>> GetTeamsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Team>> GetTeamsByIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Guid>> GetTournamentTeamIdsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
        /// <summary>Usuwa drużyny organizacji razem ze zgłoszeniami do turniejów i sztabem.</summary>
        Task RemoveTeamsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // TournamentTeam
        Task AddTournamentTeamAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default);
        Task<TournamentTeam?> GetTournamentTeamByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentTeam>> GetTournamentTeamsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentTeam>> GetTournamentTeamsByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);
        /// <summary>Śledzone (do zmiany) drużyny kategorii bez grupy — GetTournamentTeamsByTournamentIdAsync jest AsNoTracking.</summary>
        Task<IReadOnlyList<TournamentTeam>> GetUngroupedTournamentTeamsForUpdateAsync(Guid tournamentId, Guid categoryId, CancellationToken cancellationToken = default);
        Task<bool> TournamentTeamExistsAsync(Guid tournamentId, Guid teamId, Guid categoryId, CancellationToken cancellationToken = default);
        /// <summary>Śledzony wpis drużyny w turnieju i kategorii (np. do ponownego zgłoszenia po odrzuceniu).</summary>
        Task<TournamentTeam?> GetTournamentTeamForUpdateAsync(Guid tournamentId, Guid teamId, Guid categoryId, CancellationToken cancellationToken = default);
        Task<bool> AnyTournamentTeamsByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
        Task<bool> AnyTournamentTeamsByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
        Task<int> CountAcceptedTournamentTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default);

        // TeamStaff
        Task AddTeamStaffAsync(TeamStaff teamStaff, CancellationToken cancellationToken = default);
        Task RemoveTeamStaffAsync(TeamStaff teamStaff, CancellationToken cancellationToken = default);
        Task<bool> TeamStaffExistsAsync(Guid tournamentTeamId, Guid userId, TeamStaffRole role, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TeamStaff>> GetTeamStaffByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TeamStaff>> GetTeamStaffByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
