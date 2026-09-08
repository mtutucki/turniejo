using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.EventHandlers;
using Turniejo.Standings.Domain.Events;

namespace Turniejo.Standings.UnitTests.Standings
{
    public sealed class RealtimeStandingUpdatedHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldPublishStandingUpdated_WithTournamentAndGroupId()
        {
            // Arrange
            var realtimeService = Substitute.For<IRealtimeService>();
            var handler = new RealtimeStandingUpdatedHandler(realtimeService);
            var tournamentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var notification = new StandingUpdated(tournamentId, Guid.NewGuid(), groupId);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await realtimeService.Received(1).PublishStandingUpdatedAsync(tournamentId, groupId, Arg.Any<CancellationToken>());
        }
    }
}
