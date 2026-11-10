using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TournamentPlayers.GetPublicTeamSquad
{
    public sealed record GetPublicTeamSquadQuery(string Slug, Guid TournamentTeamId)
        : IQuery<IReadOnlyList<PublicSquadPlayerDto>>, IPublicRequest;
}
