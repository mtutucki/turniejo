using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.Tournaments.CreateTournament
{
    public sealed record CreateTournamentCommand(
        Guid OrganizationId,
        string Name,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        int TeamLimit,
        string? Description) : ICommand<CreateTournamentResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
