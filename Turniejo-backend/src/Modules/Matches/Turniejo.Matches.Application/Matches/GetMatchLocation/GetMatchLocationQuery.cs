using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.GetMatchLocation
{
    // UWAGA: to znany, osobny gap z audytu (nie naprawiony tym markerem) — endpoint jest współdzielony
    // przez uczestników turnieju i rodziców (PlayerGuardian), którzy nie mają wspólnego statycznego
    // scope'u do wyrażenia przez IAuthorizedRequest, więc dziś dowolny zalogowany user widzi lokalizację
    // dowolnego meczu. Docelowa poprawka: guard sprawdzający Tournament member LUB guardian zawodnika
    // w tym meczu (analogicznie do PlayerAccessGuard/RealtimeAccessGuard).
    public sealed record GetMatchLocationQuery(Guid MatchId) : IQuery<MatchLocationDto>, IAuthenticatedOnlyRequest;
}
