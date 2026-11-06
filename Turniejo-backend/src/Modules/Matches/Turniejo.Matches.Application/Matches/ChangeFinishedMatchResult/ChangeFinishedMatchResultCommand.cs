using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.ChangeFinishedMatchResult
{
    /// <summary>
    /// Zmiana rozstrzygnięcia zakończonego meczu (np. na walkower albo karne). Sędzia może to zrobić
    /// w oknie poprawek, organizator także później. Opcjonalny nowy wynik: brakujące gole dopisujemy
    /// bez strzelca, nadmiarowe cofamy (najpierw te bez strzelca).
    /// </summary>
    public sealed record ChangeFinishedMatchResultCommand(
        Guid TournamentId,
        Guid MatchId,
        MatchResultType ResultType,
        Guid? WinnerTournamentTeamId,
        int? HomePenaltyScore = null,
        int? AwayPenaltyScore = null,
        int? HomeScore = null,
        int? AwayScore = null) : ICommand<ChangeFinishedMatchResultResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}
