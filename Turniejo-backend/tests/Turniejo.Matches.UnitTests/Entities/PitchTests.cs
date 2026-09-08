using FluentAssertions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Matches.UnitTests.Entities
{
    public sealed class PitchTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidTournamentId => Guid.NewGuid();
        private static Guid ValidUserId => Guid.NewGuid();
        private const string ValidName = "Boisko 1";

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var tournamentId = ValidTournamentId;

            // Act
            var result = Pitch.Create(ValidId, tournamentId, ValidName, "Boczne", ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentId.Should().Be(tournamentId);
            result.Value.Name.Should().Be(ValidName);
            result.Value.Description.Should().Be("Boczne");
            result.Value.IsActive.Should().BeTrue();
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = Pitch.Create(Guid.Empty, ValidTournamentId, ValidName, null, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenTournamentIdIsEmpty()
        {
            var result = Pitch.Create(ValidId, Guid.Empty, ValidName, null, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentId");
        }

        [Fact]
        public void Create_ShouldFail_WhenNameIsEmpty()
        {
            var result = Pitch.Create(ValidId, ValidTournamentId, string.Empty, null, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
        }

        [Fact]
        public void Create_ShouldAddPitchAddedEvent()
        {
            var result = Pitch.Create(ValidId, ValidTournamentId, ValidName, null, ValidUserId);
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is PitchAdded);
        }

        [Fact]
        public void UpdateDetails_ShouldUpdatePitch_WhenValid()
        {
            // Arrange
            var pitch = Pitch.Create(ValidId, ValidTournamentId, ValidName, null, ValidUserId).Value;

            // Act
            var result = pitch.UpdateDetails("Boisko 2", "Główne", false);

            // Assert
            result.IsSuccess.Should().BeTrue();
            pitch.Name.Should().Be("Boisko 2");
            pitch.Description.Should().Be("Główne");
            pitch.IsActive.Should().BeFalse();
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenNameIsEmpty()
        {
            // Arrange
            var pitch = Pitch.Create(ValidId, ValidTournamentId, ValidName, null, ValidUserId).Value;

            // Act
            var result = pitch.UpdateDetails(string.Empty, null, true);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
        }
    }
}
