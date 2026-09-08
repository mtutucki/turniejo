using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.AssignMatchPitch
{
    public sealed record AssignMatchPitchCommand(
        Guid TournamentId,
        Guid MatchId,
        Guid? PitchId) : ICommand<AssignMatchPitchResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }
}
