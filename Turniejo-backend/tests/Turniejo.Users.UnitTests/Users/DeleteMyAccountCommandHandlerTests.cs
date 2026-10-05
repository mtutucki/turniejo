using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.DeleteMyAccount;
using Turniejo.Users.Domain.Entities;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.UnitTests.Users;

public sealed class DeleteMyAccountCommandHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly IUsersDbContext _dbContext = Substitute.For<IUsersDbContext>();
    private readonly IKeycloakAdminService _keycloak = Substitute.For<IKeycloakAdminService>();
    private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();
    private readonly DeleteMyAccountCommandHandler _handler;

    public DeleteMyAccountCommandHandlerTests()
    {
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.UserId.Returns(_userId);
        _handler = new DeleteMyAccountCommandHandler(_dbContext, currentUser, _keycloak, _playerModule);
    }

    private User SeedUser()
    {
        var user = User.Create(_userId, "rodzic@turniejo.pl", "Anna", "Nowak").Value;
        _dbContext.GetUserByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Handle_ShouldAnonymizeAndRemoveEverything_ForParent()
    {
        var user = SeedUser();
        var parentRole = UserRoleAssignment.Create(_userId, UserRole.Parent, RoleScopeType.Player, Guid.NewGuid()).Value;
        _dbContext.GetUserRolesAsync(_userId, Arg.Any<CancellationToken>()).Returns([parentRole]);

        var result = await _handler.Handle(new DeleteMyAccountCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Deleted);
        await _playerModule.Received(1).RemoveGuardianshipsAsync(_userId, Arg.Any<CancellationToken>());
        _dbContext.Received(1).RemoveUserRoleAssignments(Arg.Is<IEnumerable<UserRoleAssignment>>(r => r.Contains(parentRole)));
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _keycloak.Received(1).DeleteUserAsync(_userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFailWithConflict_WhenSoleOrganizerOfOrganization()
    {
        var user = SeedUser();
        var organizationId = Guid.NewGuid();
        var organizerRole = UserRoleAssignment.Create(_userId, UserRole.Organizer, RoleScopeType.Organization, organizationId).Value;
        _dbContext.GetUserRolesAsync(_userId, Arg.Any<CancellationToken>()).Returns([organizerRole]);
        _dbContext.GetUserRoleAssignmentsByScopeAsync(RoleScopeType.Organization, organizationId, Arg.Any<CancellationToken>())
            .Returns([organizerRole]);

        var result = await _handler.Handle(new DeleteMyAccountCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conflict.Account");
        user.Status.Should().Be(UserStatus.Active);
        await _keycloak.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldOnlyRetryKeycloak_WhenAlreadyAnonymized()
    {
        var user = SeedUser();
        user.Anonymize();

        var result = await _handler.Handle(new DeleteMyAccountCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _keycloak.Received(1).DeleteUserAsync(_userId, Arg.Any<CancellationToken>());
    }
}
