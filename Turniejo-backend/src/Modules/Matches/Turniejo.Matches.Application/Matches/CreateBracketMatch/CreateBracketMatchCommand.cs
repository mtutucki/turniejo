using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.CreateBracketMatch
{
    public sealed record CreateBracketMatchCommand(
        Guid TournamentId,
        Guid? CategoryId,
        Guid HomeTournamentTeamId,
        Guid AwayTournamentTeamId,
        StageType StageType) : ICommand<CreateBracketMatchResponse>, IPublicRequest;
}
