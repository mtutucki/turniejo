using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.StartMatch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class StartMatchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly StartMatchCommandHandler _handler;

        public StartMatchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new StartMatchCommandHandler(_dbContext, _tournamentModule, _currentUserContext);
        }

        private static Match CreateFakeMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_AndRecordMatchEvent_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new StartMatchCommand(tournamentId, match.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Live);
            await _dbContext.Received(1).AddMatchEventAsync(
                Arg.Is<MatchEvent>(e => e.MatchId == match.Id && e.Type == MatchEventType.MatchStarted),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new StartMatchCommand(Guid.NewGuid(), matchId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchAlreadyLive()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new StartMatchCommand(tournamentId, match.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        [Fact]
        public async Task Handle_ShouldMarkTournamentStarted_AfterSavingTheMatch()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var userId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);

            var result = await _handler.Handle(new StartMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            Received.InOrder(() =>
            {
                _dbContext.SaveChangesAsync(Arg.Any<CancellationToken>());
                _tournamentModule.MarkTournamentStartedAsync(tournamentId, userId, Arg.Any<CancellationToken>());
            });
        }

        [Fact]
        public async Task Handle_ShouldNotTouchTournament_WhenMatchCannotStart()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            match.Start(Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new StartMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            await _tournamentModule.DidNotReceive().MarkTournamentStartedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
