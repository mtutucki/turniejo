using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetAssignedMatches;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetAssignedMatchesQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetAssignedMatchesQueryHandler _handler;

        public GetAssignedMatchesQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new GetAssignedMatchesQueryHandler(_dbContext, _userModule, _currentUserContext);
        }

        private static Match CreateFakeMatch(Guid refereeId)
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.AssignReferee(refereeId, Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldReturnOnlyMatchesAssignedToCurrentUser()
        {
            // Arrange
            var refereeId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(refereeId);
            var match = CreateFakeMatch(refereeId);
            _userModule.GetUserRolesAsync(refereeId, Arg.Any<CancellationToken>())
                .Returns([new UserRoleDto(refereeId, "Referee", "Match", match.Id)]);
            _dbContext.GetMatchesByIdsAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(match.Id)), Arg.Any<CancellationToken>())
                .Returns([match]);

            // Act
            var result = await _handler.Handle(new GetAssignedMatchesQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle(m => m.Id == match.Id);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoRefereeRoleAssignments()
        {
            // Arrange
            var refereeId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(refereeId);
            _userModule.GetUserRolesAsync(refereeId, Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var result = await _handler.Handle(new GetAssignedMatchesQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
            await _dbContext.DidNotReceive().GetMatchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldIgnoreNonMatchScopedOrNonRefereeRoles()
        {
            // Zamyka lukę z audytu: lista musi filtrować po Role==Referee i ScopeType==Match,
            // nie łapać każdej roli usera.
            var userId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);
            _userModule.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
                .Returns([
                    new UserRoleDto(userId, "Organizer", "Tournament", Guid.NewGuid()),
                    new UserRoleDto(userId, "Referee", "Tournament", Guid.NewGuid())
                ]);

            // Act
            var result = await _handler.Handle(new GetAssignedMatchesQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
            await _dbContext.DidNotReceive().GetMatchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }
    }
}
