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

            // Fail-closed: brak turnieju/ustawień pod danym Id to nie "brak zdania", tylko brak
            // podstaw do przyznania dostępu. Wcześniej to gałąź "return" (fail-open) była furtką,
            // przez którą sfałszowany/nieistniejący TournamentId omijał blokadę edycji składu.
            if (tournament == null || settings == null)
                throw new UnauthorizedException();

            var tournamentHasStarted = tournament.Status is "InProgress" or "Finished";
            if (tournamentHasStarted && !settings.CanCoachEditSquadAfterStart)
                throw new UnauthorizedException();
        }
    }
}
