using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Entities;
using Turniejo.Users.Infrastructure;

namespace Turniejo.Users.UnitTests
{
    public sealed class AuthorizationServiceTests
    {
        private readonly IUsersDbContext _dbContext;
        private readonly AuthorizationService _service;

        public AuthorizationServiceTests()
        {
            _dbContext = Substitute.For<IUsersDbContext>();
            _service = new AuthorizationService(_dbContext);
        }

        [Fact]
        public async Task HasAnyRoleAsync_ShouldReturnTrue_WhenUserHasOneOfAllowedRolesInScope()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var role = UserRoleAssignment.Create(userId, UserRole.Organizer, RoleScopeType.Tournament, tournamentId).Value;
            _dbContext.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new List<UserRoleAssignment> { role });

            // Act
            var result = await _service.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Tournament), tournamentId, [nameof(UserRole.Organizer)]);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task HasAnyRoleAsync_ShouldReturnFalse_WhenUserHasNoRoleInScope()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            _dbContext.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new List<UserRoleAssignment>());

            // Act
            var result = await _service.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Tournament), tournamentId, [nameof(UserRole.Organizer)]);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HasAnyRoleAsync_ShouldReturnFalse_WhenUserHasRoleInDifferentScopeId()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var otherTournamentId = Guid.NewGuid();
            var role = UserRoleAssignment.Create(userId, UserRole.Organizer, RoleScopeType.Tournament, otherTournamentId).Value;
            _dbContext.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new List<UserRoleAssignment> { role });

            // Act
            var result = await _service.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Tournament), tournamentId, [nameof(UserRole.Organizer)]);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HasAnyRoleAsync_ShouldReturnFalse_WhenUserHasRoleNotInAllowedList()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var role = UserRoleAssignment.Create(userId, UserRole.Viewer, RoleScopeType.Tournament, tournamentId).Value;
            _dbContext.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new List<UserRoleAssignment> { role });

            // Act
            var result = await _service.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Tournament), tournamentId, [nameof(UserRole.Organizer)]);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HasAnyRoleAsync_ShouldReturnTrue_WhenUserHasAnyOfMultipleAllowedRoles()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var role = UserRoleAssignment.Create(userId, UserRole.Referee, RoleScopeType.Tournament, tournamentId).Value;
            _dbContext.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new List<UserRoleAssignment> { role });

            // Act
            var result = await _service.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Tournament), tournamentId,
                [nameof(UserRole.Organizer), nameof(UserRole.Referee), nameof(UserRole.Coach), nameof(UserRole.Viewer)]);

            // Assert
            result.Should().BeTrue();
        }
    }
}
