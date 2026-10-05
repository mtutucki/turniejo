using MediatR;
using NSubstitute;
using Turniejo.Brackets.Application.Brackets.AdvanceBracketWinner;
using Turniejo.Brackets.Application.EventHandlers;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class MatchScoreCorrectedHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldTriggerAdvanceBracketWinner_WhenWinnerIsPresent()
        {
            // Zamyka lukę z audytu: korekta wyniku zakończonego meczu w oknie edycji mogła zmienić
            // zwycięzcę, ale drabinka słuchała tylko MatchFinished, więc pozostawał w niej awansowany,
            // już nieaktualny zespół.
            var sender = Substitute.For<ISender>();
            var handler = new MatchScoreCorrectedHandler(sender);
            var matchId = Guid.NewGuid();
            var winnerId = Guid.NewGuid();
            var notification = new MatchScoreCorrected(matchId, Guid.NewGuid(), null, 1, 3, winnerId, Guid.NewGuid());

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.Received(1).Send(
                Arg.Is<AdvanceBracketWinnerCommand>(c => c.MatchId == matchId && c.WinnerTournamentTeamId == winnerId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenWinnerIsMissing()
        {
            // Korekta grupowego meczu (remis, brak zwycięzcy) nie dotyczy drabinki.
            var sender = Substitute.For<ISender>();
            var handler = new MatchScoreCorrectedHandler(sender);
            var notification = new MatchScoreCorrected(Guid.NewGuid(), Guid.NewGuid(), null, 1, 1, null, Guid.NewGuid());

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.DidNotReceive().Send(Arg.Any<AdvanceBracketWinnerCommand>(), Arg.Any<CancellationToken>());
        }
    }
}
