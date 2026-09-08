using MediatR;
using NSubstitute;
using Turniejo.Brackets.Application.Brackets.AdvanceBracketWinner;
using Turniejo.Brackets.Application.EventHandlers;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class MatchFinishedHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldTriggerAdvanceBracketWinner_WithMatchIdAndWinner()
        {
            // Arrange
            var sender = Substitute.For<ISender>();
            var handler = new MatchFinishedHandler(sender);
            var matchId = Guid.NewGuid();
            var winnerId = Guid.NewGuid();
            var notification = new MatchFinished(matchId, Guid.NewGuid(), null, "Regular", winnerId, DateTimeOffset.UtcNow, Guid.NewGuid(), 2, 1);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.Received(1).Send(
                Arg.Is<AdvanceBracketWinnerCommand>(c => c.MatchId == matchId && c.WinnerTournamentTeamId == winnerId),
                Arg.Any<CancellationToken>());
        }
    }
}
