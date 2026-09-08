using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.TournamentTeams.GetTeamPlayerStats;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetTeamPlayerStatsQueryHandlerTests
    {
        private readonly IMatchModule _matchModule;
        private readonly IPlayerModule _playerModule;
        private readonly GetTeamPlayerStatsQueryHandler _handler;

        public GetTeamPlayerStatsQueryHandlerTests()
        {
            _matchModule = Substitute.For<IMatchModule>();
            _playerModule = Substitute.For<IPlayerModule>();
            _handler = new GetTeamPlayerStatsQueryHandler(_matchModule, _playerModule);
        }

        [Fact]
        public async Task Handle_ShouldAggregateGoalsAssistsAndCards_PerPlayer()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var scorerId = Guid.NewGuid();
            var assisterId = Guid.NewGuid();

            var roster = new List<TournamentTeamPlayerDto>
            {
                new(Guid.NewGuid(), tournamentTeamId, scorerId, "Jan", "Kowalski", 9, "Forward", "Active"),
                new(Guid.NewGuid(), tournamentTeamId, assisterId, "Piotr", "Nowak", 7, "Midfielder", "Active"),
            };
            _playerModule.GetTournamentTeamPlayersAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns(roster);

            var events = new List<TeamMatchEventDto>
            {
                new(Guid.NewGuid(), "Goal", scorerId, assisterId),
                new(Guid.NewGuid(), "YellowCard", scorerId, null),
                new(Guid.NewGuid(), "Assist", assisterId, scorerId),
            };
            _matchModule.GetMatchEventsByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns(events);

            // Act
            var result = await _handler.Handle(new GetTeamPlayerStatsQuery(tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var scorerStats = result.Value.Single(s => s.PlayerId == scorerId);
            scorerStats.Goals.Should().Be(1);
            scorerStats.YellowCards.Should().Be(1);

            var assisterStats = result.Value.Single(s => s.PlayerId == assisterId);
            // Assist counted twice: once via the dedicated "Assist" event and once via the Goal's RelatedPlayerId.
            assisterStats.Assists.Should().Be(2);
        }
    }
}
