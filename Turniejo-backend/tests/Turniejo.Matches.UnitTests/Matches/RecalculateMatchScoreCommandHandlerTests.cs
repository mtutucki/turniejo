using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class RecalculateMatchScoreCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly RecalculateMatchScoreCommandHandler _handler;

        public RecalculateMatchScoreCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new RecalculateMatchScoreCommandHandler(_dbContext);
        }

        private static Match CreateLiveMatch()
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldCountGoalsAndPenaltyGoalsForScoringTeam()
        {
            // Arrange
            var match = CreateLiveMatch();
            var events = new List<MatchEvent>
            {
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value,
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.PenaltyGoal, 20, Guid.NewGuid(), match.HomeTournamentTeamId).Value,
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 30, Guid.NewGuid(), match.AwayTournamentTeamId).Value,
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.YellowCard, 40, Guid.NewGuid(), match.HomeTournamentTeamId).Value
            };
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(events);

            // Act
            var result = await _handler.Handle(new RecalculateMatchScoreCommand(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(2);
            result.Value.AwayScore.Should().Be(1);
            match.HomeScore.Should().Be(2);
            match.AwayScore.Should().Be(1);
        }

        [Fact]
        public async Task Handle_ShouldCreditOwnGoalToOpposingTeam()
        {
            // Arrange
            var match = CreateLiveMatch();
            var events = new List<MatchEvent>
            {
                // Home player scores an own goal -> counts for Away.
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.OwnGoal, 15, Guid.NewGuid(), match.HomeTournamentTeamId).Value
            };
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(events);

            // Act
            var result = await _handler.Handle(new RecalculateMatchScoreCommand(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(0);
            result.Value.AwayScore.Should().Be(1);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new RecalculateMatchScoreCommand(matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
