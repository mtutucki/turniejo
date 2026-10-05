using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Stats.GetPublicTopAssists
{
    public sealed record GetPublicTopAssistsQuery(string Slug, int Top = 10) : IQuery<IReadOnlyList<PlayerRankingEntryDto>>, IPublicRequest;
}
