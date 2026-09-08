using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchById;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchByIdQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly GetMatchByIdQueryHandler _handler;

        public GetMatchByIdQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new GetMatchByIdQueryHandler(_dbContext);
        }

        private static Match CreateFakeMatch(Guid tournamentId) =>
            Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;

        [Fact]
        public async Task Handle_ShouldReturnMappedDto_WhenMatchExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            // Act
            var result = await _handler.Handle(new GetMatchByIdQuery(tournamentId, match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(match.Id);
            result.Value.Status.Should().Be(match.Status.ToString());
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenMatchNotExist()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchByIdQuery(Guid.NewGuid(), matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenMatchBelongsToDifferentTournament()
        {
            // Arrange
            var match = CreateFakeMatch(Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            // Act
            var result = await _handler.Handle(new GetMatchByIdQuery(Guid.NewGuid(), match.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
