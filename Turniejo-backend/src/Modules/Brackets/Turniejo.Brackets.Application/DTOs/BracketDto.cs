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
        Guid? LoserGoesToNodeId,
        // Slot (Home/Away) w meczu, do którego przechodzi zwycięzca — pozwala ułożyć drabinkę stabilnie
        // (pary zasilające ten sam mecz obok siebie); węzeł nie ma własnej pozycji.
        string? WinnerGoesToSlot = null,
        // Dla slotów GroupPosition: "1. miejsce Grupa A" zamiast ogólnego "z fazy grupowej".
        Guid? HomeSourceGroupId = null,
        int? HomeSourcePosition = null,
        Guid? AwaySourceGroupId = null,
        int? AwaySourcePosition = null,
        // Termin zaplanowany dla meczu, który jeszcze nie powstał (czeka na drużyny z poprzedniej rundy).
        Guid? PlannedPitchId = null,
        DateTimeOffset? PlannedAt = null,
        // Sędzia wskazany z góry dla tego meczu (przejdzie na mecz, gdy powstanie).
        Guid? PlannedRefereeId = null);

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
