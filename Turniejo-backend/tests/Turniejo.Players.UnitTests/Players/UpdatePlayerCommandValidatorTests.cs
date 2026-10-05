using FluentAssertions;
using Turniejo.Players.Application.Players.UpdatePlayer;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class UpdatePlayerCommandValidatorTests
    {
        private readonly UpdatePlayerCommandValidator _validator = new();

        private static UpdatePlayerCommand ValidCommand(string? photoUrl = null) =>
            new(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2010, 1, 1), PlayerPosition.Forward, photoUrl);

        [Fact]
        public void Validate_ShouldSucceed_WhenPhotoUrlIsNull()
        {
            var result = _validator.Validate(ValidCommand());

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldSucceed_WhenPhotoUrlIsValidHttpsUrl()
        {
            var result = _validator.Validate(ValidCommand("https://cdn.turniejo.pl/photos/jan.jpg"));

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldFail_WhenPhotoUrlExceedsMaxLength()
        {
            var tooLong = "https://cdn.turniejo.pl/" + new string('a', 2048);

            var result = _validator.Validate(ValidCommand(tooLong));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdatePlayerCommand.PhotoUrl));
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("javascript:alert(1)")]
        [InlineData("ftp://example.com/photo.jpg")]
        public void Validate_ShouldFail_WhenPhotoUrlIsNotAnAbsoluteHttpUrl(string photoUrl)
        {
            var result = _validator.Validate(ValidCommand(photoUrl));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdatePlayerCommand.PhotoUrl));
        }
    }
}
