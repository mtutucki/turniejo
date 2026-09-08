using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TournamentPlayers.GetPublicPlayerProfile
{
    public sealed record GetPublicPlayerProfileQuery(Guid TournamentPlayerId) : IQuery<PublicPlayerProfileDto>, IPublicRequest;
}
