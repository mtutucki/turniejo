using MediatR;
using NSubstitute;
using Turniejo.Brackets.Application.Brackets.ResolveGroupPositionSlots;
using Turniejo.Brackets.Application.EventHandlers;
using Turniejo.Standings.Domain.Events;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class StandingUpdatedHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldTriggerResolveGroupPositionSlots()
        {
            // Domyka drugą część audytu: moduł Brackets w ogóle nie subskrybował StandingUpdated,
            // więc sloty GroupPosition nigdy nie rozwiązywały się same po aktualizacji tabeli.
            var sender = Substitute.For<ISender>();
            var handler = new StandingUpdatedHandler(sender);
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var notification = new StandingUpdated(tournamentId, categoryId, groupId);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            await sender.Received(1).Send(
                Arg.Is<ResolveGroupPositionSlotsCommand>(c =>
                    c.TournamentId == tournamentId && c.CategoryId == categoryId && c.GroupId == groupId),
                Arg.Any<CancellationToken>());
        }
    }
}
