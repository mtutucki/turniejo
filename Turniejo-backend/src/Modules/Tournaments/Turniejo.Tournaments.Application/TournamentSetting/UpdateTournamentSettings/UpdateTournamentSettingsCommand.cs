using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Application.TournamentSetting.UpdateTournamentSettings;

namespace Turniejo.Tournaments.Application.Tournaments.TournamentSetting.UpdateTournamentSettings
{
    public sealed record UpdateTournamentSettingsCommand(
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
        bool CanCoachEditSquadAfterStart,
        // Wartości, które formularz wczytał. Inne w bazie = ktoś zapisał zasady w międzyczasie (QA v3: UST-015).
        ExpectedTournamentSettings? Expected = null
        ) : ICommand<UpdateTournamentSettingsResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }

    public sealed record ExpectedTournamentSettings(
        int PointsForWin,
        int PointsForDraw,
        int PointsForLoss,
        int MatchDurationMinutes,
        int BreakDurationMinutes,
        bool IsThirdPlaceMatchEnabled,
        int RefereeEditWindowMinutes,
        int WalkoverHomeScore,
        int WalkoverAwayScore,
        bool CanCoachEditSquadAfterStart);
}
