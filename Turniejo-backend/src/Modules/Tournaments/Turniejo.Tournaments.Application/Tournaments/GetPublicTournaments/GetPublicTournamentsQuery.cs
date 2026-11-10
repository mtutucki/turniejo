using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetPublicTournaments
{
    /// <summary>Lista turniejów z włączonym portalem publicznym (strona główna dla gości).</summary>
    public sealed record GetPublicTournamentsQuery : IQuery<IReadOnlyList<PublicTournamentListItemDto>>, IPublicRequest;
}
