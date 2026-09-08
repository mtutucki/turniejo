using Turniejo.Players.Application.Authorization;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.Application.Services
{
    public sealed class SquadEditGuard(
        IAuthorizationService authorizationService,
        ITournamentModule tournamentModule)
    {
        public async Task EnsureCanEditSquadAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default)
        {
            var isOrganizer = await authorizationService.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Tournament), tournamentId, PlayerRoles.OrganizerOnly, cancellationToken);
            if (isOrganizer)
                return;

            var tournament = await tournamentModule.GetTournamentAsync(tournamentId, cancellationToken);
            var settings = await tournamentModule.GetTournamentSettingsAsync(tournamentId, cancellationToken);
            if (tournament == null || settings == null)
                return;

            var tournamentHasStarted = tournament.Status is "InProgress" or "Finished";
            if (tournamentHasStarted && !settings.CanCoachEditSquadAfterStart)
                throw new UnauthorizedException();
        }
    }
}
