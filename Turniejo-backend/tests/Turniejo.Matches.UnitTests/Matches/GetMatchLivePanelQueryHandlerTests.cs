using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchLivePanel;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchLivePanelQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IPlayerModule _playerModule;
        private readonly GetMatchLivePanelQueryHandler _handler;

        public GetMatchLivePanelQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _playerModule = Substitute.For<IPlayerModule>();
            _handler = new GetMatchLivePanelQueryHandler(_dbContext, _teamModule, _playerModule);

            _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((TournamentTeamDto?)null);
            _playerModule.GetTournamentTeamPlayersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeamPlayerDto>());
        }

        [Fact]
        public async Task Handle_ShouldReturnPanelWithTeamsAndEvents_WhenMatchExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var homeTeamId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            _teamModule.GetTournamentTeamAsync(match.HomeTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(match.HomeTournamentTeamId, tournamentId, homeTeamId, Guid.NewGuid(), null, "Accepted"));
            _teamModule.GetTeamAsync(homeTeamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(homeTeamId, "Lions", "LIO"));

            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.MatchStarted, 0, Guid.NewGuid()).Value;
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns([matchEvent]);

            // Act
            var result = await _handler.Handle(new GetMatchLivePanelQuery(tournamentId, match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeTeam.Name.Should().Be("Lions");
            result.Value.AwayTeam.Name.Should().Be("Unknown");
            result.Value.Events.Should().ContainSingle(e => e.Type == "MatchStarted");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchLivePanelQuery(Guid.NewGuid(), matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
