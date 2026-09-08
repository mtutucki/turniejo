using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Services;
using Turniejo.Teams.Application.TournamentTeams.GetTeamResults;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetTeamResultsQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly GetTeamResultsQueryHandler _handler;

        public GetTeamResultsQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            var resolver = new TeamMatchResolver(_dbContext, _matchModule);
            _handler = new GetTeamResultsQueryHandler(resolver);

            _dbContext.GetTournamentTeamByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Turniejo.Teams.Domain.Entities.TournamentTeam?)null);
        }

        [Fact]
        public async Task Handle_ShouldReturnOnlyFinishedMatches()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var matches = new List<TournamentMatchDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Scheduled", DateTimeOffset.UtcNow.AddDays(1), tournamentTeamId, Guid.NewGuid(), 0, 0),
                new(Guid.NewGuid(), Guid.NewGuid(), "Finished", DateTimeOffset.UtcNow.AddDays(-1), tournamentTeamId, Guid.NewGuid(), 2, 0),
            };
            _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(matches);

            // Act
            var result = await _handler.Handle(new GetTeamResultsQuery(tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].HomeScore.Should().Be(2);
        }
    }
}
