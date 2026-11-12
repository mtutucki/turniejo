using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Pitches
{
    internal static class PitchGroupsValidation
    {
        // Grupy żyją w module Tournaments (bez kluczy obcych), więc sprawdzamy, że należą do turnieju boiska.
        public static async Task<Result> CheckAsync(
            ITournamentModule tournamentModule, Guid tournamentId, IReadOnlyList<Guid> groupIds, CancellationToken cancellationToken)
        {
            if (groupIds.Count == 0)
                return Result.Success();

            var groups = await tournamentModule.GetTournamentGroupsAsync(tournamentId, cancellationToken);
            var known = groups.Select(g => g.Id).ToHashSet();
            return groupIds.All(known.Contains)
                ? Result.Success()
                : Result.Failure(Error.Validation("GroupIds", "Some of the selected groups do not belong to this tournament"));
        }
    }
}
