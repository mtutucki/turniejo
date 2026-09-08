using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Services
{
    // Niektóre widoki meczu (np. lokalizacja boiska) są współdzielone przez uczestników turnieju
    // i rodziców (PlayerGuardian) zawodników grających w tym meczu — te dwie grupy nie mają
    // wspólnego statycznego scope'u do wyrażenia przez IAuthorizedRequest, więc dostęp sprawdzamy
    // ręcznie, analogicznie do PlayerAccessGuard/RealtimeAccessGuard.
    public sealed class MatchAccessGuard(
        IAuthorizationService authorizationService,
        IPlayerModule playerModule)
    {
        public async Task<bool> CanViewAsync(Match match, Guid userId, CancellationToken cancellationToken = default)
        {
            var isMember = await authorizationService.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Tournament), match.TournamentId, MatchRoles.AnyTournamentMember, cancellationToken);
            if (isMember)
                return true;

            return await playerModule.IsGuardianOfAnyPlayerOnTeamsAsync(
                userId, [match.HomeTournamentTeamId, match.AwayTournamentTeamId], cancellationToken);
        }
    }
}
