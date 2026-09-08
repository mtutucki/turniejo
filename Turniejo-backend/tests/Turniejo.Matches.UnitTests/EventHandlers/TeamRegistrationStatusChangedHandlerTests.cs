using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.EventHandlers;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Matches.UnitTests.EventHandlers
{
    public sealed class TeamRegistrationStatusChangedHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly TeamRegistrationStatusChangedHandler _handler;

        public TeamRegistrationStatusChangedHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new TeamRegistrationStatusChangedHandler(_dbContext);
        }

        private static Match CreateScheduledMatch(Guid teamId)
        {
            var match = Match.Create(
                Guid.NewGuid(), Guid.NewGuid(), null, null, teamId, Guid.NewGuid(), null,
                DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldCancelScheduledMatches_WhenTeamWithdraws()
        {
            // Zamyka lukę z audytu: wycofanie drużyny nie blokowało startu/zakończenia jej meczów.
            var teamId = Guid.NewGuid();
            var match = CreateScheduledMatch(teamId);
            _dbContext.GetCancellableMatchesByTournamentTeamIdAsync(teamId, Arg.Any<CancellationToken>()).Returns([match]);
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            match.Status.Should().Be(MatchStatus.Cancelled);
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<Turniejo.SharedKernel.Outbox.OutboxMessage>(m => m.Type.Contains("MatchCancelled")),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenStatusIsNotWithdrawn()
        {
            var teamId = Guid.NewGuid();
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Pending, RegistrationStatus.Accepted, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().GetCancellableMatchesByTournamentTeamIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenNoCancellableMatches()
        {
            var teamId = Guid.NewGuid();
            _dbContext.GetCancellableMatchesByTournamentTeamIdAsync(teamId, Arg.Any<CancellationToken>()).Returns([]);
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
