using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Pitches.GetTournamentPitches;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.UnitTests.Pitches
{
    public sealed class GetTournamentPitchesQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly GetTournamentPitchesQueryHandler _handler;

        public GetTournamentPitchesQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new GetTournamentPitchesQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenPitchesExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            _dbContext.GetPitchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Pitch> { pitch });

            // Act
            var result = await _handler.Handle(new GetTournamentPitchesQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(pitch.Id);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoPitchesExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetPitchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Pitch>());

            // Act
            var result = await _handler.Handle(new GetTournamentPitchesQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
