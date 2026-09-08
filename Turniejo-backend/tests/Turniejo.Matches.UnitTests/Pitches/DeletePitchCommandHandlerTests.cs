using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Pitches.DeletePitch;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.UnitTests.Pitches
{
    public sealed class DeletePitchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly DeletePitchCommandHandler _handler;

        public DeletePitchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new DeletePitchCommandHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            _dbContext.GetPitchByIdAsync(pitch.Id, Arg.Any<CancellationToken>()).Returns(pitch);
            var command = new DeletePitchCommand(tournamentId, pitch.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).RemovePitchAsync(pitch, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPitchNotFound()
        {
            // Arrange
            var pitchId = Guid.NewGuid();
            _dbContext.GetPitchByIdAsync(pitchId, Arg.Any<CancellationToken>()).Returns((Pitch?)null);
            var command = new DeletePitchCommand(Guid.NewGuid(), pitchId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Pitch.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPitchBelongsToDifferentTournament()
        {
            // Arrange
            var pitch = Pitch.Create(Guid.NewGuid(), Guid.NewGuid(), "Boisko 1", null, Guid.NewGuid()).Value;
            _dbContext.GetPitchByIdAsync(pitch.Id, Arg.Any<CancellationToken>()).Returns(pitch);
            var command = new DeletePitchCommand(Guid.NewGuid(), pitch.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Pitch.NotFound");
        }
    }
}
