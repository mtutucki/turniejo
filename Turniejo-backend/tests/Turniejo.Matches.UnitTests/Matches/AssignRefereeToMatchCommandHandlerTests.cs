using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Application.Matches.AssignRefereeToMatch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class AssignRefereeToMatchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AssignRefereeToMatchCommandHandler _handler;

        public AssignRefereeToMatchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _handler = new AssignRefereeToMatchCommandHandler(_dbContext, _userModule, _authorizationService, new ScheduleConflictChecker(_dbContext, Substitute.For<ITournamentModule>()), _currentUserContext);

            // domyślnie: wskazany user jest sędzią turnieju
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        private static Match CreateFakeMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_AndAssignRoleAtomically_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var refereeId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AssignRefereeToMatchCommand(tournamentId, match.Id, refereeId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().Be(refereeId);
            await _userModule.Received(1).AssignRoleAsync(
                refereeId, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, Arg.Any<CancellationToken>());
            await _userModule.DidNotReceive().RevokeRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotAssignRole_WhenUnassigningReferee()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AssignRefereeToMatchCommand(tournamentId, match.Id, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().BeNull();
            await _userModule.DidNotReceive().AssignRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldRevokePreviousRefereeRole_WhenChangingReferee()
        {
            // Zamyka lukę z audytu: zdjęty sędzia nie ma już zachować dostępu do meczu.
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var previousRefereeId = Guid.NewGuid();
            var newRefereeId = Guid.NewGuid();
            match.AssignReferee(previousRefereeId, Guid.NewGuid());
            match.ClearDomainEvents();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AssignRefereeToMatchCommand(tournamentId, match.Id, newRefereeId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().Be(newRefereeId);
            await _userModule.Received(1).RevokeRoleAsync(
                previousRefereeId, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, Arg.Any<CancellationToken>());
            await _userModule.Received(1).AssignRoleAsync(
                newRefereeId, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldRevokePreviousRefereeRole_WhenUnassigningReferee()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var previousRefereeId = Guid.NewGuid();
            match.AssignReferee(previousRefereeId, Guid.NewGuid());
            match.ClearDomainEvents();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AssignRefereeToMatchCommand(tournamentId, match.Id, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().BeNull();
            await _userModule.Received(1).RevokeRoleAsync(
                previousRefereeId, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, Arg.Any<CancellationToken>());
            await _userModule.DidNotReceive().AssignRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotRevokeOrReassign_WhenRefereeIsUnchanged()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var refereeId = Guid.NewGuid();
            match.AssignReferee(refereeId, Guid.NewGuid());
            match.ClearDomainEvents();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AssignRefereeToMatchCommand(tournamentId, match.Id, refereeId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _userModule.DidNotReceive().RevokeRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new AssignRefereeToMatchCommand(Guid.NewGuid(), matchId, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchBelongsToDifferentTournament()
        {
            // Arrange
            var match = CreateFakeMatch(Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AssignRefereeToMatchCommand(Guid.NewGuid(), match.Id, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenUserIsNotRefereeOfTournament()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var outsiderId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _authorizationService.HasAnyRoleAsync(outsiderId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);

            var result = await _handler.Handle(new AssignRefereeToMatchCommand(tournamentId, match.Id, outsiderId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.RefereeId");
            match.RefereeId.Should().BeNull();
            await _userModule.DidNotReceive().AssignRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldCheckRefereeRoleInThisTournament()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var refereeId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            await _handler.Handle(new AssignRefereeToMatchCommand(tournamentId, match.Id, refereeId), CancellationToken.None);

            await _authorizationService.Received(1).HasAnyRoleAsync(
                refereeId, "Tournament", tournamentId,
                Arg.Is<IReadOnlyCollection<string>>(r => r.Count == 1 && r.Contains("Referee")),
                Arg.Any<CancellationToken>());
        }
        [Fact]
        public async Task Handle_ShouldFail_WhenRefereeHasAnotherMatchAtTheSameTime()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var refereeId = Guid.NewGuid();
            var other = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, match.ScheduledAt, StageType.Group).Value;
            other.AssignReferee(refereeId, Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { match, other });

            var result = await _handler.Handle(new AssignRefereeToMatchCommand(tournamentId, match.Id, refereeId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("The referee already has another match at this time");
            match.RefereeId.Should().BeNull();
        }
    }
}
