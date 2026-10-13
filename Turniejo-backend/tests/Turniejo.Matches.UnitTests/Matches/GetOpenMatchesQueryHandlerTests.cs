using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetOpenMatches;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetOpenMatchesQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IUserModule _userModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetOpenMatchesQueryHandler _handler;
        private readonly Guid _userId = Guid.NewGuid();

        public GetOpenMatchesQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _userModule = Substitute.For<IUserModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _currentUserContext.UserId.Returns(_userId);
            _handler = new GetOpenMatchesQueryHandler(_dbContext, _userModule, _currentUserContext);
        }

        private static Match CreateFakeMatch(Guid tournamentId) =>
            Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;

        [Fact]
        public async Task Handle_ShouldReturnOnlyUnassignedUnfinishedMatches_FromTournamentsWhereUserIsReferee()
        {
            var tournamentId = Guid.NewGuid();
            var open = CreateFakeMatch(tournamentId);
            var liveWithoutReferee = CreateFakeMatch(tournamentId);
            liveWithoutReferee.Start(Guid.NewGuid());
            var taken = CreateFakeMatch(tournamentId);
            taken.AssignReferee(Guid.NewGuid(), Guid.NewGuid());
            var cancelled = CreateFakeMatch(tournamentId);
            cancelled.Cancel("deszcz", Guid.NewGuid());
            _userModule.GetUserRolesAsync(_userId, Arg.Any<CancellationToken>())
                .Returns([new UserRoleDto(_userId, "Referee", "Tournament", tournamentId)]);
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Match> { open, liveWithoutReferee, taken, cancelled });

            var result = await _handler.Handle(new GetOpenMatchesQuery(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Select(m => m.Id).Should().BeEquivalentTo([open.Id, liveWithoutReferee.Id]);
        }

        [Fact]
        public async Task Handle_ShouldIgnoreTournamentsWhereUserIsNotReferee()
        {
            _userModule.GetUserRolesAsync(_userId, Arg.Any<CancellationToken>())
                .Returns([
                    new UserRoleDto(_userId, "Coach", "Tournament", Guid.NewGuid()),
                    new UserRoleDto(_userId, "Referee", "Match", Guid.NewGuid())
                ]);

            var result = await _handler.Handle(new GetOpenMatchesQuery(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
            await _dbContext.DidNotReceive().GetMatchesByTournamentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
