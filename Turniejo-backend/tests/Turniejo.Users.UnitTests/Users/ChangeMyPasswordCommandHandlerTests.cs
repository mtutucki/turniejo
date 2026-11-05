using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.ChangeMyPassword;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class ChangeMyPasswordCommandHandlerTests
{
    private const string CurrentPassword = "StareHaslo123";
    private const string NewPassword = "NoweHaslo123";

    private readonly IKeycloakAdminService _keycloak = Substitute.For<IKeycloakAdminService>();
    private readonly IUsersDbContext _dbContext = Substitute.For<IUsersDbContext>();
    private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();
    private readonly User _user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
    private readonly ChangeMyPasswordCommandHandler _handler;

    public ChangeMyPasswordCommandHandlerTests()
    {
        _currentUser.UserId.Returns(_user.Id);
        _dbContext.GetUserByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _handler = new ChangeMyPasswordCommandHandler(
            _dbContext, _keycloak, _currentUser, NullLogger<ChangeMyPasswordCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldSetPasswordAndCloseSessions_WhenCurrentPasswordIsCorrect()
    {
        _keycloak.VerifyPasswordAsync(_user.Email, CurrentPassword, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(new ChangeMyPasswordCommand(CurrentPassword, NewPassword), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _keycloak.Received(1).SetPasswordAsync(_user.Id, NewPassword, Arg.Any<CancellationToken>());
        await _keycloak.Received(1).LogoutAllSessionsAsync(_user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRejectAndKeepPassword_WhenCurrentPasswordIsWrong()
    {
        _keycloak.VerifyPasswordAsync(_user.Email, CurrentPassword, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new ChangeMyPasswordCommand(CurrentPassword, NewPassword), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.CurrentPassword");
        await _keycloak.DidNotReceive().SetPasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenClosingSessionsFails()
    {
        _keycloak.VerifyPasswordAsync(_user.Email, CurrentPassword, Arg.Any<CancellationToken>()).Returns(true);
        _keycloak.LogoutAllSessionsAsync(_user.Id, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        var result = await _handler.Handle(new ChangeMyPasswordCommand(CurrentPassword, NewPassword), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldRejectNewPasswordEqualToCurrent()
    {
        var validation = new ChangeMyPasswordCommandValidator().Validate(new ChangeMyPasswordCommand(NewPassword, NewPassword));

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(ChangeMyPasswordCommand.NewPassword));
    }
}
