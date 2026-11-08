using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.ClaimMatch;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class ClaimMatchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ClaimMatchCommandHandler _handler;
        private readonly Guid _refereeId = Guid.NewGuid();

        public ClaimMatchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _currentUserContext.UserId.Returns(_refereeId);
            _handler = new ClaimMatchCommandHandler(
                _dbContext, _userModule, new ScheduleConflictChecker(_dbContext, Substitute.For<ITournamentModule>(), Substitute.For<ITeamModule>()), _currentUserContext);
        }

        private static Match CreateFakeMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldAssignMatchAndRoleToCurrentUser_WhenMatchHasNoReferee()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new ClaimMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().Be(_refereeId);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
            await _userModule.Received(1).AssignRoleAsync(
                _refereeId, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_AndNotGrantRole_WhenMatchAlreadyHasReferee()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var otherRefereeId = Guid.NewGuid();
            match.AssignReferee(otherRefereeId, Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new ClaimMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.RefereeId");
            match.RefereeId.Should().Be(otherRefereeId);
            await _userModule.DidNotReceive().AssignRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenRefereeHasAnotherMatchAtTheSameTime()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var other = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, match.ScheduledAt, StageType.Group).Value;
            other.AssignReferee(_refereeId, Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { match, other });

            var result = await _handler.Handle(new ClaimMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("The referee already has another match at this time");
            match.RefereeId.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchBelongsToDifferentTournament()
        {
            var match = CreateFakeMatch(Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new ClaimMatchCommand(Guid.NewGuid(), match.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
