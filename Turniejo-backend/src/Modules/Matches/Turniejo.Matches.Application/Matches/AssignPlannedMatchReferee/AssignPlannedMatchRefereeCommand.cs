using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.AssignPlannedMatchReferee
{
    /// <summary>Sędzia meczu pucharu, który jeszcze nie powstał. Mecz dostanie go przy utworzeniu.</summary>
    public sealed record AssignPlannedMatchRefereeCommand(
        Guid TournamentId,
        Guid NodeId,
        Guid? RefereeId) : ICommand<AssignPlannedMatchRefereeResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }

    public sealed record AssignPlannedMatchRefereeResponse(Guid NodeId);
}
