using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.GetUserRoles;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class GetUserRolesQueryHandlerTests
{
    private readonly IUsersDbContext _dbContext;
    private readonly GetUserRolesQueryHandler _handler;

    public GetUserRolesQueryHandlerTests()
    {
        _dbContext = Substitute.For<IUsersDbContext>();
        _handler = new GetUserRolesQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenUserHasNoRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserRoleAssignment>());
        var query = new GetUserRolesQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedDtos_WhenRolesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var role = UserRoleAssignment.Create(userId, UserRole.Coach, RoleScopeType.Tournament, Guid.NewGuid()).Value;
        _dbContext.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserRoleAssignment> { role });
        var query = new GetUserRolesQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(userId);
        result.Value[0].Role.Should().Be(role.Role.ToString());
    }
}