using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchLocation;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchLocationQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly GetMatchLocationQueryHandler _handler;

        public GetMatchLocationQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new GetMatchLocationQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchLocationQuery(matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnPitchDetails_WhenMatchHasPitchAssigned()
        {
            // Arrange
            var pitchId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), pitchId, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var pitch = Pitch.Create(pitchId, match.TournamentId, "Boisko 1", "Obok wejścia głównego", Guid.NewGuid()).Value;
            _dbContext.GetPitchByIdAsync(pitchId, Arg.Any<CancellationToken>()).Returns(pitch);

            // Act
            var result = await _handler.Handle(new GetMatchLocationQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PitchName.Should().Be("Boisko 1");
        }

        [Fact]
        public async Task Handle_ShouldReturnNullPitchInfo_WhenNoPitchAssigned()
        {
            // Arrange
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            // Act
            var result = await _handler.Handle(new GetMatchLocationQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PitchId.Should().BeNull();
            result.Value.PitchName.Should().BeNull();
        }
    }
}
