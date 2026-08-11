using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.AssignUserRole;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class AssignUserRoleCommandHandlerTests
{
    private readonly IUsersDbContext _dbContext;
    private readonly AssignUserRoleCommandHandler _handler;

    public AssignUserRoleCommandHandlerTests()
    {
        _dbContext = Substitute.For<IUsersDbContext>();
        _handler = new AssignUserRoleCommandHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRoleAlreadyExists()
    {
        // Arrange — idempotentne, nie błąd
        var userId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        _dbContext.UserRoleAssignmentExistsAsync(userId, UserRole.Coach, RoleScopeType.Tournament, scopeId, Arg.Any<CancellationToken>())
            .Returns(true);
        var command = new AssignUserRoleCommand(userId, UserRole.Coach, RoleScopeType.Tournament, scopeId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _dbContext.DidNotReceive().AddUserRoleAssignmentAsync(Arg.Any<UserRoleAssignment>(), Arg.Any<CancellationToken>());
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAssignRole_WhenNotExisting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        _dbContext.UserRoleAssignmentExistsAsync(userId, UserRole.Coach, RoleScopeType.Tournament, scopeId, Arg.Any<CancellationToken>())
            .Returns(false);
        var command = new AssignUserRoleCommand(userId, UserRole.Coach, RoleScopeType.Tournament, scopeId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _dbContext.Received(1).AddUserRoleAssignmentAsync(Arg.Any<UserRoleAssignment>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserIdIsEmpty()
    {
        // Arrange — UserRoleAssignment.Create() zwróci Failure
        _dbContext.UserRoleAssignmentExistsAsync(Guid.Empty, UserRole.Coach, RoleScopeType.Tournament, null, Arg.Any<CancellationToken>())
            .Returns(false);
        var command = new AssignUserRoleCommand(Guid.Empty, UserRole.Coach, RoleScopeType.Tournament, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}