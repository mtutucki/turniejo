using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Pitches
{
    internal static class PitchRefereeValidation
    {
        // Sędzia boiska musi być sędzią turnieju (rola nadana kodem), a jedna osoba prowadzi jedno boisko:
        // na dwóch boiskach naraz miałaby kolizję w każdym terminie.
        public static async Task<Result> CheckAsync(
            IMatchesDbContext dbContext,
            IAuthorizationService authorizationService,
            Guid tournamentId,
            Guid? pitchId,
            Guid? refereeId,
            CancellationToken cancellationToken)
        {
            if (refereeId is not { } id)
                return Result.Success();

            var isReferee = await authorizationService.HasAnyRoleAsync(
                id, nameof(RoleScopeType.Tournament), tournamentId, [nameof(UserRole.Referee)], cancellationToken);
            if (!isReferee)
                return Result.Failure(Error.Validation("RefereeId", "User is not a referee in this tournament"));

            var pitches = await dbContext.GetPitchesByTournamentIdAsync(tournamentId, cancellationToken);
            return pitches.Any(p => p.Id != pitchId && p.RefereeId == id)
                ? Result.Failure(Error.Validation("RefereeId", "The referee is already assigned to another pitch"))
                : Result.Success();
        }
    }
}
