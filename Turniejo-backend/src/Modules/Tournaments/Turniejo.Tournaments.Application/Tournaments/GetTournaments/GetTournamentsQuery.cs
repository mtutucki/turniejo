using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournaments
{
    public sealed record GetTournamentsQuery(Guid OrganizationId)
        : IQuery<IReadOnlyList<TournamentDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
