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
            // QA v3: GPK-006 - uzupełnienie slotów z miejsc w grupach nie odświeżało drabinki na żywo.
        [Fact]
        public async Task GPK_006_TeamsAdvancedToBracket_PublishesBracketUpdated()
        {
            var realtimeService = Substitute.For<IRealtimeService>();
            var handler = new RealtimeBracketUpdatedHandler(realtimeService);
            var tournamentId = Guid.NewGuid();

            await handler.Handle(new TeamsAdvancedToBracket(Guid.NewGuid(), tournamentId), CancellationToken.None);

            await realtimeService.Received(1).PublishBracketUpdatedAsync(tournamentId, Arg.Any<CancellationToken>());
        }
    }
}
