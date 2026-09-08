using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.GetMatchLocation
{
    // Współdzielony przez uczestników turnieju i rodziców (PlayerGuardian) zawodników grających w tym
    // meczu — te dwie grupy nie mają wspólnego statycznego scope'u do wyrażenia przez IAuthorizedRequest,
    // więc dostęp sprawdza ręcznie MatchAccessGuard w handlerze (analogicznie do PlayerAccessGuard/
    // RealtimeAccessGuard). Authenticated-only tutaj tylko po to, by pipeline wymusił zalogowanie.
    public sealed record GetMatchLocationQuery(Guid MatchId) : IQuery<MatchLocationDto>, IAuthenticatedOnlyRequest;
}
