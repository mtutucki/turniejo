using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Pitches.UpdatePitch;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.UnitTests.Pitches
{
    public sealed class UpdatePitchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly UpdatePitchCommandHandler _handler;

        public UpdatePitchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new UpdatePitchCommandHandler(_dbContext);
        }

        private static Pitch CreateFakePitch(Guid tournamentId)
        {
            var pitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            pitch.ClearDomainEvents();
            return pitch;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitch = CreateFakePitch(tournamentId);
            _dbContext.GetPitchByIdAsync(pitch.Id, Arg.Any<CancellationToken>()).Returns(pitch);
            var command = new UpdatePitchCommand(tournamentId, pitch.Id, "Boisko 2", "Główne", false);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            pitch.Name.Should().Be("Boisko 2");
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPitchNotFound()
        {
            // Arrange
            var pitchId = Guid.NewGuid();
            _dbContext.GetPitchByIdAsync(pitchId, Arg.Any<CancellationToken>()).Returns((Pitch?)null);
            var command = new UpdatePitchCommand(Guid.NewGuid(), pitchId, "Boisko 2", null, true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Pitch.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNewNameAlreadyExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitch = CreateFakePitch(tournamentId);
            _dbContext.GetPitchByIdAsync(pitch.Id, Arg.Any<CancellationToken>()).Returns(pitch);
            _dbContext.PitchNameExistsAsync(tournamentId, "Boisko 2", Arg.Any<CancellationToken>()).Returns(true);
            var command = new UpdatePitchCommand(tournamentId, pitch.Id, "Boisko 2", null, true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
