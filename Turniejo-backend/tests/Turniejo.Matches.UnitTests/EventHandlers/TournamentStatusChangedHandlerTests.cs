using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.EventHandlers;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Matches.UnitTests.EventHandlers
{
    public sealed class TournamentStatusChangedHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly TournamentStatusChangedHandler _handler;

        public TournamentStatusChangedHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new TournamentStatusChangedHandler(_dbContext);
        }

        private static Match CreateScheduledMatch() =>
            Match.Create(
                Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null,
                DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;

        [Fact]
        public async Task Handle_ShouldCancelAllCancellableMatches_WhenTournamentCancelled()
        {
            // Zamyka lukę z audytu: anulowanie turnieju nie zatrzymywało jego meczów.
            var tournamentId = Guid.NewGuid();
            var match = CreateScheduledMatch();
            match.ClearDomainEvents();
            _dbContext.GetCancellableMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns([match]);
            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), Guid.NewGuid(), TournamentStatus.InProgress, TournamentStatus.Cancelled);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            match.Status.Should().Be(MatchStatus.Cancelled);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldCancelUnplayedMatches_WhenTournamentFinished()
        {
            // Nierozpoczęte mecze dało się dalej startować w zakończonym turnieju.
            var tournamentId = Guid.NewGuid();
            var match = CreateScheduledMatch();
            match.ClearDomainEvents();
            _dbContext.GetCancellableMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns([match]);
            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), Guid.NewGuid(), TournamentStatus.InProgress, TournamentStatus.Finished);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            match.Status.Should().Be(MatchStatus.Cancelled);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenNewStatusIsNotCancelledOrFinished()
        {
            var tournamentId = Guid.NewGuid();
            var notification = new TournamentStatusChanged(tournamentId, Guid.NewGuid(), Guid.NewGuid(), TournamentStatus.Draft, TournamentStatus.RegistrationOpen);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().GetCancellableMatchesByTournamentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
