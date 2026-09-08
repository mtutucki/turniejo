using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.GetAssignedMatches
{
    // Handler filtruje wyłącznie po Match.RefereeId == CurrentUserId — brak parametrów wejściowych,
    // więc nie ma czego autoryzować poza samym zalogowaniem.
    public sealed record GetAssignedMatchesQuery : IQuery<IReadOnlyList<MatchDto>>, IAuthenticatedOnlyRequest;
}
