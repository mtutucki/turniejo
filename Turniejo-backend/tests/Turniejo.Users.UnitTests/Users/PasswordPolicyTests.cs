using FluentAssertions;
using Turniejo.Users.Application.Users.ConfirmPasswordReset;
using Turniejo.Users.Application.Users.RegisterUser;

namespace Turniejo.Users.UnitTests.Users;

public sealed class PasswordPolicyTests
{
    private readonly RegisterUserCommandValidator _registerValidator = new();
    private readonly ConfirmPasswordResetCommandValidator _resetValidator = new();

    private bool IsAcceptedOnRegister(string password) =>
        _registerValidator
            .Validate(new RegisterUserCommand("jan@turniejo.pl", password, "Jan", "Kowalski", null, AcceptTerms: true))
            .Errors.TrueForAll(e => e.PropertyName != nameof(RegisterUserCommand.Password));

    private bool IsAcceptedOnReset(string password) =>
        _resetValidator
            .Validate(new ConfirmPasswordResetCommand("token", password))
            .Errors.TrueForAll(e => e.PropertyName != nameof(ConfirmPasswordResetCommand.NewPassword));

    [Theory]
    [InlineData("NoweHaslo1")]
    [InlineData("Żółtakartka7")]
    [InlineData("dluga fraza z Cyfra 4")]
    public void Validators_ShouldAcceptPassword_WhenItMeetsPolicy(string password)
    {
        IsAcceptedOnRegister(password).Should().BeTrue();
        IsAcceptedOnReset(password).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Haslo123!")] // 9 znaków
    [InlineData("nowehaslo123")] // bez wielkiej litery
    [InlineData("NOWEHASLO123")] // bez małej litery
    [InlineData("NoweHasloBezCyfr")]
    public void Validators_ShouldRejectPassword_WhenItBreaksPolicy(string password)
    {
        IsAcceptedOnRegister(password).Should().BeFalse();
        IsAcceptedOnReset(password).Should().BeFalse();
    }

    [Fact]
    public void Validators_ShouldRejectPassword_WhenItIsLongerThan100Characters()
    {
        var password = "Aa1" + new string('x', 98);

        IsAcceptedOnRegister(password).Should().BeFalse();
        IsAcceptedOnReset(password).Should().BeFalse();
    }
}
