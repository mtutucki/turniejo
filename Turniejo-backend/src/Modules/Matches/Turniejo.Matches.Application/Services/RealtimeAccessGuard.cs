using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Services
{
    // Bramka wywoływana z MatchHub/TournamentHub (SignalR) przy dołączaniu do grupy live.
    // Huby nie mogą wymuszać [Authorize] na całej klasie, bo publiczny portal (GetPublicLiveMatchQuery)
    // celowo pozwala anonimowym widzom oglądać live mecze publicznych turniejów — więc dostęp sprawdzamy
    // tu ręcznie, tym samym warunkiem co REST: publiczny i widoczny turniej ALBO zalogowany członek turnieju.
    public sealed class RealtimeAccessGuard(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        IAuthorizationService authorizationService)
    {
        public async Task<bool> CanJoinTournamentGroupAsync(Guid tournamentId, Guid? userId, CancellationToken cancellationToken = default)
        {
            var settings = await tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, cancellationToken);
            if (settings is { IsPublic: true })
                return true;

            if (userId is null)
                return false;

            return await authorizationService.HasAnyRoleAsync(
                userId.Value, nameof(RoleScopeType.Tournament), tournamentId, MatchRoles.AnyTournamentMember, cancellationToken);
        }

        public async Task<bool> CanJoinMatchGroupAsync(Guid matchId, Guid? userId, CancellationToken cancellationToken = default)
        {
            var match = await dbContext.GetMatchByIdAsync(matchId, cancellationToken);
            if (match == null)
                return false;

            var settings = await tournamentModule.GetTournamentPublicSettingsAsync(match.TournamentId, cancellationToken);
            if (settings is { IsPublic: true, ShowLiveMatches: true })
                return true;

            if (userId is null)
                return false;

            var isMember = await authorizationService.HasAnyRoleAsync(
                userId.Value, nameof(RoleScopeType.Tournament), match.TournamentId, MatchRoles.AnyTournamentMember, cancellationToken);
            if (isMember)
                return true;

            return await authorizationService.HasAnyRoleAsync(
                userId.Value, nameof(RoleScopeType.Match), matchId, MatchRoles.RefereeOnly, cancellationToken);
        }
    }
}
