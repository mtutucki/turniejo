using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.TournamentGroups.UpdateTournamentGroup
{
    public sealed record UpdateTournamentGroupCommand(
        Guid TournamentId,
        Guid GroupId,
        string Name,
        int Order) : ICommand<UpdateTournamentGroupResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
