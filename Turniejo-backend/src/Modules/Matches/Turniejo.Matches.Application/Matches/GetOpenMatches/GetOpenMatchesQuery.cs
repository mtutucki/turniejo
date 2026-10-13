using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.GetOpenMatches
{
    // Handler sam zawęża wynik do turniejów, w których CurrentUserId jest sędzią — brak parametrów
    // wejściowych, więc nie ma czego autoryzować poza samym zalogowaniem.
    public sealed record GetOpenMatchesQuery : IQuery<IReadOnlyList<MatchDto>>, IAuthenticatedOnlyRequest;
}
