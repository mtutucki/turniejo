using Turniejo.SharedKernel.CQRS;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetPublicTeamProfile
{
    public sealed record GetPublicTeamProfileQuery(string Slug, Guid TournamentTeamId) : IQuery<PublicTeamDto>, IPublicRequest;
}
