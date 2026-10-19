using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.ConfirmPasswordReset;
using Turniejo.Users.Application.Users.RequestPasswordReset;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class ConfirmPasswordResetCommandHandlerTests
{
    private const string NewPassword = "NoweHaslo123";

    private readonly IKeycloakAdminService _keycloak = Substitute.For<IKeycloakAdminService>();
    private readonly IUsersDbContext _dbContext = Substitute.For<IUsersDbContext>();
    private readonly ConfirmPasswordResetCommandHandler _handler;

    public ConfirmPasswordResetCommandHandlerTests()
    {
        _handler = new ConfirmPasswordResetCommandHandler(
            _keycloak, _dbContext, NullLogger<ConfirmPasswordResetCommandHandler>.Instance);
    }

    private (User User, PasswordResetToken Stored, string Token) SeedToken()
    {
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        var (token, hash) = PasswordResetTokenGenerator.Generate();
        var stored = PasswordResetToken.Create(user.Id, hash);

        _dbContext.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _dbContext.GetPasswordResetTokenByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(stored);
        return (user, stored, token);
    }

    private async Task AssertRejectedAsync(string token)
    {
        var result = await _handler.Handle(new ConfirmPasswordResetCommand(token, NewPassword), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Token");
        await _keycloak.DidNotReceive().SetPasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetPassword_MarkTokenUsed_AndCloseSessions_WhenTokenIsValid()
    {
        var (user, stored, token) = SeedToken();

        var result = await _handler.Handle(new ConfirmPasswordResetCommand(token, NewPassword), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _keycloak.Received(1).SetPasswordAsync(user.Id, NewPassword, Arg.Any<CancellationToken>());
        await _keycloak.Received(1).LogoutAllSessionsAsync(user.Id, Arg.Any<CancellationToken>());
        stored.UsedAt.Should().NotBeNull();
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTokenIsUnknown()
    {
        _dbContext.GetPasswordResetTokenByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PasswordResetToken?)null);

        await AssertRejectedAsync("nie-ma-takiego-tokenu");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTokenWasAlreadyUsed()
    {
        var (_, stored, token) = SeedToken();
        stored.MarkUsed();

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTokenHasExpired()
    {
        var (_, stored, token) = SeedToken();
        // ExpiresAt ustawia wyłącznie Create — cofamy je refleksją zamiast czekać godzinę.
        typeof(PasswordResetToken).GetProperty(nameof(PasswordResetToken.ExpiresAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(stored, DateTimeOffset.UtcNow.AddSeconds(-1));

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAccountWasDeletedAfterRequest()
    {
        var (user, _, token) = SeedToken();
        user.Anonymize();

        await AssertRejectedAsync(token);
    }

    [Fact]
    public async Task Handle_ShouldKeepTokenUsable_WhenKeycloakRejectsPasswordChange()
    {
        // Awaria Keycloaka nie może spalić linku — user ponawia tym samym.
        var (user, stored, token) = SeedToken();
        _keycloak.SetPasswordAsync(user.Id, NewPassword, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Keycloak unavailable")));

        var act = () => _handler.Handle(new ConfirmPasswordResetCommand(token, NewPassword), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        stored.UsedAt.Should().BeNull();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenClosingSessionsFails()
    {
        var (user, stored, token) = SeedToken();
        _keycloak.LogoutAllSessionsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Keycloak unavailable")));

        var result = await _handler.Handle(new ConfirmPasswordResetCommand(token, NewPassword), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        stored.UsedAt.Should().NotBeNull();
    }
}
