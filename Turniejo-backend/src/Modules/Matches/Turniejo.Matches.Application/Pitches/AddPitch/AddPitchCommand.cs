using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Pitches.AddPitch
{
    public sealed record AddPitchCommand(
        Guid TournamentId,
        string Name,
        string? Description,
        IReadOnlyList<Guid>? GroupIds = null) : ICommand<AddPitchResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }
}
