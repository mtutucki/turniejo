namespace Turniejo.Brackets.Application.DTOs
{
    public sealed record BracketNodeDto(
        Guid Id,
        Guid BracketRoundId,
        Guid? MatchId,
        string HomeSourceType,
        Guid? HomeTournamentTeamId,
        string AwaySourceType,
        Guid? AwayTournamentTeamId,
        Guid? WinnerGoesToNodeId,
        Guid? LoserGoesToNodeId);

    public sealed record BracketRoundDto(
        Guid Id,
        int RoundNumber,
        string RoundType,
        string Name,
        IReadOnlyList<BracketNodeDto> Nodes);

    public sealed record BracketDto(
        Guid Id,
        Guid TournamentId,
        IReadOnlyList<BracketRoundDto> Rounds);
}
