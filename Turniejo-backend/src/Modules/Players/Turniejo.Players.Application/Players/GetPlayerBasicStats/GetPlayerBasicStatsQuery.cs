using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.GetPlayerBasicStats
{
    // Handler weryfikuje PlayerAccessGuard.CanViewAsync (self/guardian/Organizer/Coach), tak samo
    // jak GetPlayerByIdQuery — to te same dane osobowe zawodnika, tylko w formie statystyk.
    public sealed record GetPlayerBasicStatsQuery(Guid PlayerId) : IQuery<PlayerBasicStatsDto>, IAuthenticatedOnlyRequest;
}
