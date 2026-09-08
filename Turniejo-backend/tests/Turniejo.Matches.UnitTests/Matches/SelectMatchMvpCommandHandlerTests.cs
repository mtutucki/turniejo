using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.SelectMatchMvp;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class SelectMatchMvpCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly SelectMatchMvpCommandHandler _handler;

        public SelectMatchMvpCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new SelectMatchMvpCommandHandler(_dbContext, _currentUserContext);

            _dbContext.GetActiveMatchEventsByTypeAsync(Arg.Any<Guid>(), MatchEventType.MvpSelected, Arg.Any<CancellationToken>())
                .Returns(new List<MatchEvent>());
        }

        private static Match CreateLiveMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var playerId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new SelectMatchMvpCommand(tournamentId, match.Id, match.HomeTournamentTeamId, playerId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddMatchEventAsync(
                Arg.Is<MatchEvent>(e => e.Type == MatchEventType.MvpSelected && e.PlayerId == playerId),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<Turniejo.SharedKernel.Outbox.OutboxMessage>(m => m.Type.Contains("MatchEventAdded")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldMarkPreviousMvpDeleted_WhenReplacingSelection()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var previousMvpUserId = Guid.NewGuid();
            var previousMvp = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.MvpSelected, 0, previousMvpUserId, match.AwayTournamentTeamId, Guid.NewGuid()).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetActiveMatchEventsByTypeAsync(match.Id, MatchEventType.MvpSelected, Arg.Any<CancellationToken>())
                .Returns(new List<MatchEvent> { previousMvp });
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new SelectMatchMvpCommand(tournamentId, match.Id, match.HomeTournamentTeamId, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            previousMvp.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new SelectMatchMvpCommand(Guid.NewGuid(), matchId, Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
