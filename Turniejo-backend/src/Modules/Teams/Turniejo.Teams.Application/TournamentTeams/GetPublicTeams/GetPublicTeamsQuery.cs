using Turniejo.SharedKernel.CQRS;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetPublicTeams
{
    public sealed record GetPublicTeamsQuery(string Slug) : IQuery<IReadOnlyList<PublicTeamDto>>, IPublicRequest;
}
