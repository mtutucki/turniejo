using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Stats.GetPublicTopScorers
{
    public sealed record GetPublicTopScorersQuery(string Slug, int Top = 10) : IQuery<IReadOnlyList<PlayerRankingEntryDto>>, IPublicRequest;
}
