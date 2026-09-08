using NSubstitute;
using Turniejo.Matches.Application.EventHandlers;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.EventHandlers
{
    public sealed class RealtimeMatchScoreChangedHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldPublishMatchScoreChanged_WithMatchIdAndScore()
        {
            // Arrange
            var realtimeService = Substitute.For<IRealtimeService>();
            var handler = new RealtimeMatchScoreChangedHandler(realtimeService);
            var matchId = Guid.NewGuid();
            var notification = new MatchScoreChanged(matchId, Guid.NewGuid(), Guid.NewGuid(), 2, 1);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await realtimeService.Received(1).PublishMatchScoreChangedAsync(matchId, 2, 1, Arg.Any<CancellationToken>());
        }
    }
}
