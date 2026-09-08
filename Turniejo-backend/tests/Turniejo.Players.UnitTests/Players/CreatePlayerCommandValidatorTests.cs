using FluentAssertions;
using Turniejo.Players.Application.Players.CreatePlayer;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class CreatePlayerCommandValidatorTests
    {
        private readonly CreatePlayerCommandValidator _validator = new();

        private static CreatePlayerCommand ValidCommand(string? photoUrl = null) =>
            new("Jan", "Kowalski", new DateTime(2010, 1, 1), PlayerPosition.Forward, photoUrl, null);

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
            // Zamyka lukę z audytu: kolumna PhotoUrl ma MaxLength(2048) w bazie, walidator nie miał
            // żadnego limitu.
            var tooLong = "https://cdn.turniejo.pl/" + new string('a', 2048);

            var result = _validator.Validate(ValidCommand(tooLong));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreatePlayerCommand.PhotoUrl));
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("javascript:alert(1)")]
        [InlineData("ftp://example.com/photo.jpg")]
        [InlineData("data:image/png;base64,aGVsbG8=")]
        public void Validate_ShouldFail_WhenPhotoUrlIsNotAnAbsoluteHttpUrl(string photoUrl)
        {
            var result = _validator.Validate(ValidCommand(photoUrl));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreatePlayerCommand.PhotoUrl));
        }
    }
}
