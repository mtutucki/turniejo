using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.RegisterUser;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests
{
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IUsersDbContext _dbContext;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _keycloakAdminService = Substitute.For<IKeycloakAdminService>();
        _dbContext = Substitute.For<IUsersDbContext>();
        _handler = new RegisterUserCommandHandler(_keycloakAdminService, _dbContext);
    }

    private static RegisterUserCommand ValidCommand() =>
        new("jan@turniejo.pl", "Password123!", "Jan", "Kowalski", null);

    [Fact]
    public async Task Handle_ShouldFail_WhenEmailAlreadyExistsInKeycloak()
    {
        // Arrange
        _keycloakAdminService.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Guid>(new InvalidOperationException("CONFLICT: email taken")));

        // Act
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Email");
        await _dbContext.DidNotReceive().AddUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDataIsValid()
    {
        // Arrange
        var keycloakUserId = Guid.NewGuid();
        _keycloakAdminService.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(keycloakUserId);

        // Act
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(keycloakUserId);
    }

    [Fact]
    public async Task Handle_ShouldAddUserAndOutboxMessage_WhenSuccessful()
    {
        // Arrange
        var keycloakUserId = Guid.NewGuid();
        _keycloakAdminService.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(keycloakUserId);

        // Act
        await _handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        await _dbContext.Received(1).AddUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}