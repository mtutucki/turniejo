using NSubstitute;
using Turniejo.Matches.Application.EventHandlers;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.EventHandlers
{
    public sealed class RealtimeMatchEventAddedHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldPublishMatchEventAdded()
        {
            // Arrange
            var realtimeService = Substitute.For<IRealtimeService>();
            var handler = new RealtimeMatchEventAddedHandler(realtimeService);
            var matchId = Guid.NewGuid();
            var matchEventId = Guid.NewGuid();
            var notification = new MatchEventAdded(matchEventId, matchId, Guid.NewGuid(), "Goal");

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await realtimeService.Received(1).PublishMatchEventAddedAsync(matchId, matchEventId, "Goal", Arg.Any<CancellationToken>());
        }
    }
}
