using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.ReleaseMatch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class ReleaseMatchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ReleaseMatchCommandHandler _handler;
        private readonly Guid _refereeId = Guid.NewGuid();

        public ReleaseMatchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _currentUserContext.UserId.Returns(_refereeId);
            _handler = new ReleaseMatchCommandHandler(_dbContext, _userModule, _currentUserContext);
        }

        private static Match CreateFakeMatch(Guid tournamentId, Guid refereeId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.AssignReferee(refereeId, Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldClearRefereeAndRevokeRole_WhenCalledByMatchReferee()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId, _refereeId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new ReleaseMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().BeNull();
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
            await _userModule.Received(1).RevokeRoleAsync(
                _refereeId, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhileMatchIsLive()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId, _refereeId);
            match.Start(_refereeId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new ReleaseMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().BeNull();
            match.Status.Should().Be(MatchStatus.Live);
        }

        [Fact]
        public async Task Handle_ShouldFail_AndKeepRole_WhenMatchBelongsToAnotherReferee()
        {
            var tournamentId = Guid.NewGuid();
            var otherRefereeId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId, otherRefereeId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new ReleaseMatchCommand(tournamentId, match.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.RefereeId");
            match.RefereeId.Should().Be(otherRefereeId);
            await _userModule.DidNotReceive().RevokeRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
