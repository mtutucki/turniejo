using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.Services
{
    // Player to globalny profil bez jednego "właściciela" scope'u (może grać w wielu drużynach/turniejach),
    // więc dostęp nie da się wyrazić jako pojedynczy IAuthorizedRequest — sprawdzamy dynamicznie wszystkie
    // relacje, przez które ktoś legalnie ma wgląd/edycję profilu: konto własne, opiekun, Organizer/Coach
    // drużyny, do której zawodnik jest przypisany. Brak dopasowania = brak dostępu (fail-closed).
    public sealed class PlayerAccessGuard(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        IAuthorizationService authorizationService)
    {
        public async Task<bool> CanViewAsync(Player player, Guid userId, CancellationToken cancellationToken = default)
        {
            if (player.UserId == userId)
                return true;

            var guardians = await dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, cancellationToken);
            if (guardians.Any(g => g.UserId == userId))
                return true;

            return await HasStaffAccessAsync(player.Id, userId, cancellationToken);
        }

        public async Task<bool> CanEditAsync(Player player, Guid userId, CancellationToken cancellationToken = default)
        {
            if (player.UserId == userId)
                return true;

            var guardians = await dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, cancellationToken);
            if (guardians.Any(g => g.UserId == userId && g.CanEdit))
                return true;

            return await HasStaffAccessAsync(player.Id, userId, cancellationToken);
        }

        private async Task<bool> HasStaffAccessAsync(Guid playerId, Guid userId, CancellationToken cancellationToken)
        {
            var tournamentPlayers = await dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, cancellationToken);

            foreach (var tournamentPlayer in tournamentPlayers)
            {
                var isCoach = await authorizationService.HasAnyRoleAsync(
                    userId, nameof(RoleScopeType.TournamentTeam), tournamentPlayer.TournamentTeamId, PlayerRoles.CoachOnly, cancellationToken);
                if (isCoach)
                    return true;

                var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentPlayer.TournamentTeamId, cancellationToken);
                if (tournamentTeam == null)
                    continue;

                var isOrganizer = await authorizationService.HasAnyRoleAsync(
                    userId, nameof(RoleScopeType.Tournament), tournamentTeam.TournamentId, PlayerRoles.OrganizerOnly, cancellationToken);
                if (isOrganizer)
                    return true;
            }

            return false;
        }
    }
}
