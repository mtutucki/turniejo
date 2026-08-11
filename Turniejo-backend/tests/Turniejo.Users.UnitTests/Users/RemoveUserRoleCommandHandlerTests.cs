using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.RemoveUserRole;

namespace Turniejo.Users.UnitTests.Users;

public sealed class RemoveUserRoleCommandHandlerTests
{
    private readonly IUsersDbContext _dbContext;
    private readonly RemoveUserRoleCommandHandler _handler;

    public RemoveUserRoleCommandHandlerTests()
    {
        _dbContext = Substitute.For<IUsersDbContext>();
        _handler = new RemoveUserRoleCommandHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldRemoveRoleAndSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var command = new RemoveUserRoleCommand(userId, UserRole.Referee, RoleScopeType.Match, scopeId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _dbContext.Received(1).RemoveUserRoleAssignmentAsync(
            userId, UserRole.Referee, RoleScopeType.Match, scopeId, Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}