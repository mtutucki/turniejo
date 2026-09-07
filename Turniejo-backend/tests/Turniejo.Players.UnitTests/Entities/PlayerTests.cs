using FluentAssertions;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.Players.Domain.Events;

namespace Turniejo.Players.UnitTests.Entities
{
    public sealed class PlayerTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidCreatedByUserId => Guid.NewGuid();
        private const string ValidFirstName = "Jan";
        private const string ValidLastName = "Kowalski";
        private static DateTime ValidBirthDate => new(2012, 5, 1);

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var validId = ValidId;

            // Act
            var result = Player.Create(validId, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, "photo.png", null, ValidCreatedByUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(validId);
            result.Value.FirstName.Should().Be(ValidFirstName);
            result.Value.LastName.Should().Be(ValidLastName);
            result.Value.BirthDate.Should().Be(ValidBirthDate);
            result.Value.DefaultPosition.Should().Be(PlayerPosition.Forward);
            result.Value.PhotoUrl.Should().Be("photo.png");
            result.Value.UserId.Should().BeNull();
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = Player.Create(Guid.Empty, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenFirstNameIsEmpty()
        {
            var result = Player.Create(ValidId, string.Empty, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.FirstName");
        }

        [Fact]
        public void Create_ShouldFail_WhenLastNameIsEmpty()
        {
            var result = Player.Create(ValidId, ValidFirstName, string.Empty, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.LastName");
        }

        [Fact]
        public void Create_ShouldFail_WhenBirthDateIsInFuture()
        {
            var result = Player.Create(ValidId, ValidFirstName, ValidLastName, DateTime.UtcNow.Date.AddDays(1), PlayerPosition.Forward, null, null, ValidCreatedByUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.BirthDate");
        }

        [Fact]
        public void Create_ShouldAddPlayerCreatedEvent()
        {
            var result = Player.Create(ValidId, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId);
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is PlayerCreated);
        }

        [Fact]
        public void UpdateDetails_ShouldUpdatePlayer_WhenValid()
        {
            // Arrange
            var player = Player.Create(ValidId, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId).Value;

            // Act
            var result = player.UpdateDetails("Adam", "Nowak", ValidBirthDate.AddDays(1), PlayerPosition.Goalkeeper, "new-photo.png");

            // Assert
            result.IsSuccess.Should().BeTrue();
            player.FirstName.Should().Be("Adam");
            player.LastName.Should().Be("Nowak");
            player.DefaultPosition.Should().Be(PlayerPosition.Goalkeeper);
            player.PhotoUrl.Should().Be("new-photo.png");
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenBirthDateIsInFuture()
        {
            // Arrange
            var player = Player.Create(ValidId, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId).Value;

            // Act
            var result = player.UpdateDetails(ValidFirstName, ValidLastName, DateTime.UtcNow.Date.AddDays(1), PlayerPosition.Forward, null);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.BirthDate");
        }

        [Fact]
        public void UpdateDetails_ShouldAddPlayerUpdatedEvent()
        {
            // Arrange
            var player = Player.Create(ValidId, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId).Value;
            player.ClearDomainEvents();

            // Act
            var result = player.UpdateDetails("Adam", "Nowak", ValidBirthDate, PlayerPosition.Forward, null);

            // Assert
            result.IsSuccess.Should().BeTrue();
            player.DomainEvents.Should().ContainSingle(e => e is PlayerUpdated);
        }

        [Fact]
        public void AssignUser_ShouldSetUserId_WhenValid()
        {
            // Arrange
            var player = Player.Create(ValidId, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId).Value;
            var userId = Guid.NewGuid();

            // Act
            var result = player.AssignUser(userId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            player.UserId.Should().Be(userId);
        }

        [Fact]
        public void AssignUser_ShouldFail_WhenUserIdIsEmpty()
        {
            // Arrange
            var player = Player.Create(ValidId, ValidFirstName, ValidLastName, ValidBirthDate, PlayerPosition.Forward, null, null, ValidCreatedByUserId).Value;

            // Act
            var result = player.AssignUser(Guid.Empty);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.UserId");
        }
    }
}
