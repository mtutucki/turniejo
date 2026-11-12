using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Pitches.UpdatePitch
{
    public sealed record UpdatePitchCommand(
        Guid TournamentId,
        Guid PitchId,
        string Name,
        string? Description,
        bool IsActive,
        IReadOnlyList<Guid>? GroupIds = null,
        Guid? RefereeId = null) : ICommand<UpdatePitchResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }
}
