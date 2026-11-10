using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.TournamentGroups.AddTournamentGroup
{
    public sealed record AddTournamentGroupCommand(
        Guid TournamentId,
        Guid CategoryId,
        string Name,
        int Order,
        int Stage = 1) : ICommand<AddTournamentGroupResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
