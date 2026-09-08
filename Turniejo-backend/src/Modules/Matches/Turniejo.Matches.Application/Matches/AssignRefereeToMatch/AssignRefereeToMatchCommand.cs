using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.AssignRefereeToMatch
{
    public sealed record AssignRefereeToMatchCommand(
        Guid TournamentId,
        Guid MatchId,
        Guid? RefereeId) : ICommand<AssignRefereeToMatchResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }
}
