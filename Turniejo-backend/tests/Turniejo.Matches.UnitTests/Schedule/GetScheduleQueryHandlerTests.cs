using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Schedule.GetSchedule;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class GetScheduleQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly GetScheduleQueryHandler _handler;

        public GetScheduleQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new GetScheduleQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_OrderedByScheduledAt()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var later = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(2), StageType.Group).Value;
            var earlier = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;

            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Match> { later, earlier });

            // Act
            var result = await _handler.Handle(new GetScheduleQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].Id.Should().Be(earlier.Id);
            result.Value[1].Id.Should().Be(later.Id);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoMatchesExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Match>());

            // Act
            var result = await _handler.Handle(new GetScheduleQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
