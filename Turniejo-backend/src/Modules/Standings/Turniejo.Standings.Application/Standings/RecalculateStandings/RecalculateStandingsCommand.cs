using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Standings.Application.Standings.RecalculateStandings
{
    internal sealed record RecalculateStandingsCommand(Guid GroupId) : ICommand<RecalculateStandingsResponse>, IPublicRequest;
}
