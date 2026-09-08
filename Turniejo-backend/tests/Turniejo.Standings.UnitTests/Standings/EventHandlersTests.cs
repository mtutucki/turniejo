using MediatR;
using NSubstitute;
using Turniejo.Matches.Domain.Events;
using Turniejo.Standings.Application.EventHandlers;
using Turniejo.Standings.Application.Standings.RecalculateStandings;

namespace Turniejo.Standings.UnitTests.Standings
{
    public sealed class EventHandlersTests
    {
        [Fact]
        public async Task MatchScoreChangedHandler_ShouldTriggerRecalculation_WhenGroupIdPresent()
        {
            // Arrange
            var sender = Substitute.For<ISender>();
            var handler = new MatchScoreChangedHandler(sender);
            var groupId = Guid.NewGuid();
            var notification = new MatchScoreChanged(Guid.NewGuid(), Guid.NewGuid(), groupId, 1, 0);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.Received(1).Send(
                Arg.Is<RecalculateStandingsCommand>(c => c.GroupId == groupId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchScoreChangedHandler_ShouldDoNothing_WhenGroupIdMissing()
        {
            // Arrange
            var sender = Substitute.For<ISender>();
            var handler = new MatchScoreChangedHandler(sender);
            var notification = new MatchScoreChanged(Guid.NewGuid(), Guid.NewGuid(), null, 1, 0);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.DidNotReceive().Send(Arg.Any<RecalculateStandingsCommand>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchFinishedHandler_ShouldTriggerRecalculation_WhenGroupIdPresent()
        {
            // Arrange
            var sender = Substitute.For<ISender>();
            var handler = new MatchFinishedHandler(sender);
            var groupId = Guid.NewGuid();
            var notification = new MatchFinished(Guid.NewGuid(), Guid.NewGuid(), groupId, "Regular", null, DateTimeOffset.UtcNow, Guid.NewGuid(), 2, 2);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.Received(1).Send(
                Arg.Is<RecalculateStandingsCommand>(c => c.GroupId == groupId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchFinishedHandler_ShouldDoNothing_WhenGroupIdMissing()
        {
            // Arrange
            var sender = Substitute.For<ISender>();
            var handler = new MatchFinishedHandler(sender);
            var notification = new MatchFinished(Guid.NewGuid(), Guid.NewGuid(), null, "Regular", null, DateTimeOffset.UtcNow, Guid.NewGuid(), 0, 0);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.DidNotReceive().Send(Arg.Any<RecalculateStandingsCommand>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchScoreCorrectedHandler_ShouldTriggerRecalculation_WhenGroupIdPresent()
        {
            // Zamyka lukę z audytu: korekta wyniku po zakończeniu meczu (CorrectFinishedMatchScoreCommand)
            // ma odświeżać tabelę tak samo jak zwykła zmiana wyniku czy zakończenie meczu.
            var sender = Substitute.For<ISender>();
            var handler = new MatchScoreCorrectedHandler(sender);
            var groupId = Guid.NewGuid();
            var notification = new MatchScoreCorrected(Guid.NewGuid(), Guid.NewGuid(), groupId, 3, 2, null, Guid.NewGuid());

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.Received(1).Send(
                Arg.Is<RecalculateStandingsCommand>(c => c.GroupId == groupId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchScoreCorrectedHandler_ShouldDoNothing_WhenGroupIdMissing()
        {
            // Arrange
            var sender = Substitute.For<ISender>();
            var handler = new MatchScoreCorrectedHandler(sender);
            var notification = new MatchScoreCorrected(Guid.NewGuid(), Guid.NewGuid(), null, 3, 2, null, Guid.NewGuid());

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.DidNotReceive().Send(Arg.Any<RecalculateStandingsCommand>(), Arg.Any<CancellationToken>());
        }
    }
}
