using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Brackets.Application.Brackets.AdvanceBracketWinner
{
    internal sealed record AdvanceBracketWinnerCommand(Guid MatchId, Guid? WinnerTournamentTeamId)
        : ICommand<AdvanceBracketWinnerResponse>, IPublicRequest;
}
