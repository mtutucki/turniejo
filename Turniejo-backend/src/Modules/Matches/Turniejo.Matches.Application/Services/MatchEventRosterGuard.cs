using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Services
{
    // Bez tego dało się dopisać gola/kartkę/asystę dowolnemu PlayerId, w tym zawodnikowi drużyny
    // przeciwnej — handlery sprawdzały tylko, że TournamentTeamId gra w tym meczu, nigdy że PlayerId
    // faktycznie należy do składu tej drużyny. Dla OwnGoal to wciąż własna drużyna sprawcy (patrz
    // RecalculateMatchScoreCommandHandler: TournamentTeamId eventu OwnGoal = drużyna gracza, nie
    // drużyna, której zapisuje się punkt), więc reguła jest taka sama dla każdego typu eventu.
    public sealed class MatchEventRosterGuard(IPlayerModule playerModule)
    {
        public async Task<Result> EnsureOnRosterAsync(
            Guid tournamentTeamId, Guid? playerId, string fieldName, CancellationToken cancellationToken = default)
        {
            if (!playerId.HasValue)
                return Result.Success();

            var roster = await playerModule.GetTournamentTeamPlayersAsync(tournamentTeamId, cancellationToken);
            var isOnRoster = roster.Any(p => p.PlayerId == playerId.Value);

            return isOnRoster
                ? Result.Success()
                : Result.Failure(Error.Validation(fieldName, "Player is not on the roster of this team"));
        }
    }
}
