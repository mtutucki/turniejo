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

        [Fact]
        public async Task Handle_ShouldResolveTeamNames_WhenTeamsExist()
        {
            // TeamMatchResolver nie ma własnego pliku testowego — jest testowany tylko pośrednio przez
            // handlery, które go używają, ale żaden istniejący test nie asertował nazw drużyn (w
            // konstruktorze GetTournamentTeamByIdAsync jest zamockowane na null, więc rozwiązywanie
            // nazwy nigdy nie było faktycznie sprawdzone, tylko przypadkiem trafiało w fallback "Unknown").
            var tournamentTeamId = Guid.NewGuid();
            var awayTeamId = Guid.NewGuid();
            var match = new TournamentMatchDto(Guid.NewGuid(), Guid.NewGuid(), "Finished", DateTimeOffset.UtcNow.AddDays(-1), tournamentTeamId, awayTeamId, 3, 1);
            _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([match]);

            var homeTeam = Turniejo.Teams.Domain.Entities.TournamentTeam.Create(tournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            var awayTournamentTeam = Turniejo.Teams.Domain.Entities.TournamentTeam.Create(awayTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns(homeTeam);
            _dbContext.GetTournamentTeamByIdAsync(awayTeamId, Arg.Any<CancellationToken>()).Returns(awayTournamentTeam);
            _dbContext.GetTeamByIdAsync(homeTeam.TeamId, Arg.Any<CancellationToken>())
                .Returns(Turniejo.Teams.Domain.Entities.Team.Create(homeTeam.TeamId, Guid.NewGuid(), Guid.NewGuid(), "Lions", "LIO", null).Value);
            _dbContext.GetTeamByIdAsync(awayTournamentTeam.TeamId, Arg.Any<CancellationToken>())
                .Returns(Turniejo.Teams.Domain.Entities.Team.Create(awayTournamentTeam.TeamId, Guid.NewGuid(), Guid.NewGuid(), "Tigers", "TIG", null).Value);

            // Act
            var result = await _handler.Handle(new GetTeamResultsQuery(tournamentTeamId), CancellationToken.None);

            // Assert
            result.Value[0].HomeTeamName.Should().Be("Lions");
            result.Value[0].AwayTeamName.Should().Be("Tigers");
        }
    }
}
