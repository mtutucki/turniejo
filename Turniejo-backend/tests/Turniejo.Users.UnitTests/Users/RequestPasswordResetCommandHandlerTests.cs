using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.RequestPasswordReset;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class RequestPasswordResetCommandHandlerTests
{
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IUsersDbContext _dbContext;
    private readonly RequestPasswordResetCommandHandler _handler;

    public RequestPasswordResetCommandHandlerTests()
    {
        _keycloakAdminService = Substitute.For<IKeycloakAdminService>();
        _dbContext = Substitute.For<IUsersDbContext>();
        _handler = new RequestPasswordResetCommandHandler(_keycloakAdminService, _dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenUserDoesNotExist()
    {
        // Arrange — celowo nie ujawniamy że email nie istnieje
        _dbContext.GetUserByEmailAsync("nieznany@turniejo.pl", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        var command = new RequestPasswordResetCommand("nieznany@turniejo.pl");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _keycloakAdminService.DidNotReceive().SendPasswordResetEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendResetEmail_WhenUserExists()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        var command = new RequestPasswordResetCommand(user.Email);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _keycloakAdminService.Received(1).SendPasswordResetEmailAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenKeycloakThrows()
    {
        // Arrange — błąd Keycloak nie powinien wyciekać do usera
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _keycloakAdminService.SendPasswordResetEmailAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Keycloak unavailable")));
        var command = new RequestPasswordResetCommand(user.Email);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}