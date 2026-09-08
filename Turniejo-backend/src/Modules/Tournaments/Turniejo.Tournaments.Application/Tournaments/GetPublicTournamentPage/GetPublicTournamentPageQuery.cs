using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetPublicTournamentPage
{
    public sealed record GetPublicTournamentPageQuery(string Slug) : IQuery<PublicTournamentPageDto>, IPublicRequest;
}
