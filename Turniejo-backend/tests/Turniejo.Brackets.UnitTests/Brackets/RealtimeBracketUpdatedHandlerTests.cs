using NSubstitute;
using Turniejo.Brackets.Application.EventHandlers;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class RealtimeBracketUpdatedHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldPublishBracketUpdated_WithTournamentId()
        {
            // Arrange
            var realtimeService = Substitute.For<IRealtimeService>();
            var handler = new RealtimeBracketUpdatedHandler(realtimeService);
            var tournamentId = Guid.NewGuid();
            var notification = new BracketUpdated(Guid.NewGuid(), tournamentId);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await realtimeService.Received(1).PublishBracketUpdatedAsync(tournamentId, Arg.Any<CancellationToken>());
        }
    }
}
