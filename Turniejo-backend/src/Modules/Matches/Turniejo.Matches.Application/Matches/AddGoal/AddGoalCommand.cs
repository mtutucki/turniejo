using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.AddGoal
{
    public sealed record AddGoalCommand(
        Guid TournamentId,
        Guid MatchId,
        Guid TournamentTeamId,
        Guid? PlayerId,
        Guid? AssistPlayerId,
        MatchEventType GoalType,
        int Minute,
        int? Second) : ICommand<AddGoalResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}
