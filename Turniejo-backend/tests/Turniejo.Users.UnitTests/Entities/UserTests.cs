using FluentAssertions;
using Turniejo.Users.Domain.Entities;
using Turniejo.Users.Domain.Enums;
using Turniejo.Users.Domain.Events;

namespace Turniejo.Users.UnitTests.Entities;

public sealed class UserTests
{
    private static Guid ValidId => Guid.NewGuid();
    private const string ValidEmail = "jan.kowalski@turniejo.pl";
    private const string ValidFirstName = "Jan";
    private const string ValidLastName = "Kowalski";

    [Fact]
    public void Create_ShouldSucceed_WhenAllDataIsValid()
    {
        // Arrange
        var id = ValidId;

        // Act
        var result = User.Create(id, ValidEmail, ValidFirstName, ValidLastName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.Email.Should().Be(ValidEmail);
        result.Value.FirstName.Should().Be(ValidFirstName);
        result.Value.LastName.Should().Be(ValidLastName);
        result.Value.PhoneNumber.Should().BeNull();
        result.Value.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Create_ShouldSetStatusToActive()
    {
        // Act
        var result = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName);

        // Assert
        result.Value.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Create_ShouldSetPhoneNumber_WhenProvided()
    {
        // Arrange
        const string phoneNumber = "+48123456789";

        // Act
        var result = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName, phoneNumber);

        // Assert
        result.Value.PhoneNumber.Should().Be(phoneNumber);
    }

    [Fact]
    public void Create_ShouldAddUserRegisteredEvent()
    {
        // Arrange
        var id = ValidId;

        // Act
        var result = User.Create(id, ValidEmail, ValidFirstName, ValidLastName);

        // Assert
        result.Value.DomainEvents.Should().HaveCount(1);
        result.Value.DomainEvents[0].Should().BeOfType<UserRegistered>();

        var @event = (UserRegistered)result.Value.DomainEvents[0];
        @event.UserId.Should().Be(id);
        @event.Email.Should().Be(ValidEmail);
        @event.FirstName.Should().Be(ValidFirstName);
        @event.LastName.Should().Be(ValidLastName);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var user = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName).Value;

        // Act
        user.ClearDomainEvents();

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldFail_WhenIdIsEmpty()
    {
        // Act
        var result = User.Create(Guid.Empty, ValidEmail, ValidFirstName, ValidLastName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Id");
    }


    [Fact]
    public void Create_ShouldFail_WhenEmailIsEmpty()
    {
        // Act
        var result = User.Create(ValidId, string.Empty, ValidFirstName, ValidLastName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Email");
    }

    [Fact]
    public void Create_ShouldFail_WhenEmailIsWhitespace()
    {
        // Act
        var result = User.Create(ValidId, "   ", ValidFirstName, ValidLastName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Email");
    }


    [Fact]
    public void Create_ShouldFail_WhenFirstNameIsEmpty()
    {
        // Act
        var result = User.Create(ValidId, ValidEmail, string.Empty, ValidLastName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.FirstName");
    }

    [Fact]
    public void Create_ShouldFail_WhenFirstNameIsWhitespace()
    {
        // Act
        var result = User.Create(ValidId, ValidEmail, "   ", ValidLastName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.FirstName");
    }


    [Fact]
    public void Create_ShouldFail_WhenLastNameIsEmpty()
    {
        // Act
        var result = User.Create(ValidId, ValidEmail, ValidFirstName, string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.LastName");
    }

    [Fact]
    public void Create_ShouldFail_WhenLastNameIsWhitespace()
    {
        // Act
        var result = User.Create(ValidId, ValidEmail, ValidFirstName, "   ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.LastName");
    }

    [Fact]
    public void Create_ShouldRecordTermsAcceptance_WhenVersionGiven()
    {
        var user = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName, acceptedTermsVersion: "2026-09-30").Value;

        user.TermsVersion.Should().Be("2026-09-30");
        user.TermsAcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public void Anonymize_ShouldRemovePersonalData_AndMarkDeleted()
    {
        var user = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName, "+48123456789", acceptedTermsVersion: "2026-09-30").Value;

        user.Anonymize();

        user.Status.Should().Be(UserStatus.Deleted);
        user.DeletedAt.Should().NotBeNull();
        user.Email.Should().NotContain("kowalski");
        user.FirstName.Should().NotBe(ValidFirstName);
        user.LastName.Should().NotBe(ValidLastName);
        user.PhoneNumber.Should().BeNull();
        // Dowód akceptacji regulaminu zostaje (rozliczalność).
        user.TermsVersion.Should().Be("2026-09-30");
    }

    [Fact]
    public void MarkReleaseSeen_MovesOnlyForward()
    {
        var user = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName).Value;

        user.MarkReleaseSeen("1.2.0").IsSuccess.Should().BeTrue();
        user.MarkReleaseSeen("1.10.0").IsSuccess.Should().BeTrue();
        user.MarkReleaseSeen("1.9.5").IsSuccess.Should().BeTrue();

        user.SeenReleaseVersion.Should().Be("1.10.0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("v1.2.0")]
    [InlineData("1.2.0-3-gabc1234")]
    public void MarkReleaseSeen_RejectsAnythingButXYZ(string version)
    {
        var user = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName).Value;

        user.MarkReleaseSeen(version).IsFailure.Should().BeTrue();
        user.SeenReleaseVersion.Should().BeNull();
    }

    [Fact]
    public void ThemePreference_DefaultsToSystem_AndCanBeChanged()
    {
        var user = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName).Value;
        user.ThemePreference.Should().Be(ThemePreference.System);

        user.SetThemePreference(ThemePreference.Light).IsSuccess.Should().BeTrue();

        user.ThemePreference.Should().Be(ThemePreference.Light);
    }

    [Fact]
    public void SetThemePreference_RejectsUnknownValue()
    {
        var user = User.Create(ValidId, ValidEmail, ValidFirstName, ValidLastName).Value;

        user.SetThemePreference((ThemePreference)7).IsFailure.Should().BeTrue();
        user.ThemePreference.Should().Be(ThemePreference.System);
    }
}
