using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchScore;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchScoreQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly GetMatchScoreQueryHandler _handler;

        public GetMatchScoreQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new GetMatchScoreQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnCurrentScore_WhenMatchExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.UpdateScore(3, 2);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            // Act
            var result = await _handler.Handle(new GetMatchScoreQuery(tournamentId, match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(3);
            result.Value.AwayScore.Should().Be(2);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchScoreQuery(Guid.NewGuid(), matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
