using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.GetTournamentMembers;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class GetTournamentMembersQueryHandlerTests
{
    private readonly IUsersDbContext _dbContext;
    private readonly GetTournamentMembersQueryHandler _handler;

    public GetTournamentMembersQueryHandlerTests()
    {
        _dbContext = Substitute.For<IUsersDbContext>();
        _handler = new GetTournamentMembersQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldGroupRolesPerUser()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var user = User.Create(Guid.NewGuid(), "trener@turniejo.pl", "Jan", "Kowalski", null).Value;
        var assignments = new List<UserRoleAssignment>
        {
            UserRoleAssignment.Create(user.Id, UserRole.Coach, RoleScopeType.Tournament, tournamentId).Value,
            UserRoleAssignment.Create(user.Id, UserRole.Viewer, RoleScopeType.Tournament, tournamentId).Value,
        };
        _dbContext.GetUserRoleAssignmentsByScopeAsync(RoleScopeType.Tournament, tournamentId, Arg.Any<CancellationToken>())
            .Returns(assignments);
        _dbContext.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await _handler.Handle(new GetTournamentMembersQuery(tournamentId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Email.Should().Be("trener@turniejo.pl");
        result.Value[0].Roles.Should().BeEquivalentTo("Coach", "Viewer");
    }

    [Fact]
    public async Task Handle_ShouldSkipUsersThatNoLongerExist()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var assignment = UserRoleAssignment.Create(Guid.NewGuid(), UserRole.Viewer, RoleScopeType.Tournament, tournamentId).Value;
        _dbContext.GetUserRoleAssignmentsByScopeAsync(RoleScopeType.Tournament, tournamentId, Arg.Any<CancellationToken>())
            .Returns(new List<UserRoleAssignment> { assignment });

        // Act
        var result = await _handler.Handle(new GetTournamentMembersQuery(tournamentId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
