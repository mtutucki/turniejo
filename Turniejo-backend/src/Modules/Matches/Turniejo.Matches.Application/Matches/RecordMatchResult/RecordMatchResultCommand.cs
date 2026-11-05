using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.RecordMatchResult
{
    /// <summary>Wpisanie wyniku meczu, który nie był prowadzony na żywo (bez zegara).</summary>
    public sealed record RecordMatchResultCommand(
        Guid TournamentId,
        Guid MatchId,
        int HomeScore,
        int AwayScore,
        MatchResultType ResultType,
        Guid? WinnerTournamentTeamId,
        int? HomePenaltyScore = null,
        int? AwayPenaltyScore = null) : ICommand<RecordMatchResultResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public const int MaxGoals = 99;

        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}
