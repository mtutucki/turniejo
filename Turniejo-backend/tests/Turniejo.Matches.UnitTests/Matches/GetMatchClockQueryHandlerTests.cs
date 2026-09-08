using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchClock;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchClockQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly GetMatchClockQueryHandler _handler;

        public GetMatchClockQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new GetMatchClockQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnZeroElapsed_WhenMatchNotStarted()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            // Act
            var result = await _handler.Handle(new GetMatchClockQuery(tournamentId, match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ElapsedSeconds.Should().Be(0);
            result.Value.Status.Should().Be("Scheduled");
        }

        [Fact]
        public async Task Handle_ShouldFreezeElapsed_AtPausedAt_WhenPaused()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(-1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.Pause(Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            // Act
            var result = await _handler.Handle(new GetMatchClockQuery(tournamentId, match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var expected = (int)(match.PausedAt!.Value - match.StartedAt!.Value).TotalSeconds - match.TotalPausedSeconds;
            result.Value.ElapsedSeconds.Should().Be(expected);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchClockQuery(Guid.NewGuid(), matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
