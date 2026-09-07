namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record TournamentSettingsDto(
        Guid TournamentId,
        int PointsForWin,
        int PointsForDraw,
        int PointsForLoss,
        int MatchDurationMinutes,
        int BreakDurationMinutes,
        bool IsThirdPlaceMatchEnabled,
        int RefereeEditWindowMinutes,
        int WalkoverHomeScore,
        int WalkoverAwayScore,
        bool CanCoachEditSquadAfterStart
        );
}