namespace Turniejo.Matches.Application.Matches.SetGoalPlayers
{
    public sealed record SetGoalPlayersResponse(Guid MatchEventId, Guid? PlayerId, Guid? AssistPlayerId);
}
