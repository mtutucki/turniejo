using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Stats.GetPublicMvpRanking
{
    public sealed record GetPublicMvpRankingQuery(string Slug, int Top = 10) : IQuery<IReadOnlyList<PlayerRankingEntryDto>>, IPublicRequest;
}
