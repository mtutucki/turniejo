using FluentAssertions;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Entities;

public sealed class UserRoleAssignmentTests
{
    private static Guid ValidUserId => Guid.NewGuid();
    private const UserRole ValidRole = UserRole.Organizer;
    private const RoleScopeType ValidScopeType = RoleScopeType.Tournament;


    [Fact]
    public void Create_ShouldSucceed_WhenAllDataIsValid()
    {
        // Arrange
        var userId = ValidUserId;
        var scopeId = Guid.NewGuid();

        // Act
        var result = UserRoleAssignment.Create(userId, ValidRole, ValidScopeType, scopeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Role.Should().Be(ValidRole);
        result.Value.ScopeType.Should().Be(ValidScopeType);
        result.Value.ScopeId.Should().Be(scopeId);
    }

    [Fact]
    public void Create_ShouldSucceed_WhenScopeIdIsNull()
    {
        // Act
        var result = UserRoleAssignment.Create(ValidUserId, ValidRole, ValidScopeType, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScopeId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldGenerateNewId()
    {
        // Act
        var result1 = UserRoleAssignment.Create(ValidUserId, ValidRole, ValidScopeType);
        var result2 = UserRoleAssignment.Create(ValidUserId, ValidRole, ValidScopeType);

        // Assert
        result1.Value.Id.Should().NotBe(result2.Value.Id);
    }

    [Theory]
    [InlineData(UserRole.Organizer, RoleScopeType.Organization)]
    [InlineData(UserRole.Coach, RoleScopeType.TournamentTeam)]
    [InlineData(UserRole.Referee, RoleScopeType.Match)]
    [InlineData(UserRole.Parent, RoleScopeType.Player)]
    public void Create_ShouldSucceed_ForAllRoleScopeCombinations(
        UserRole role, RoleScopeType scopeType)
    {
        // Act
        var result = UserRoleAssignment.Create(ValidUserId, role, scopeType, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(role);
        result.Value.ScopeType.Should().Be(scopeType);
    }

    // ── Walidacja UserId ──────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldFail_WhenUserIdIsEmpty()
    {
        // Act
        var result = UserRoleAssignment.Create(
            Guid.Empty, ValidRole, ValidScopeType);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.UserId");
    }
}