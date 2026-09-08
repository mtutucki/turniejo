using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.Services
{
    // MyChildMatchResolver nie miał własnego pliku testowego — jest reużywany przez
    // GetMyChildResultsQuery/GetMyChildScheduleQuery/GetPlayerMatchHistoryQuery, ale żaden z tych
    // handlerów nigdy nie asertował nazw drużyn/turnieju (GetTournamentTeamAsync/GetTournamentAsync
    // były w testach zamockowane na null "przy okazji", nie celowo), więc rozwiązywanie nazw i cache
    // per-wywołanie było w praktyce nietestowane.
    public sealed class MyChildMatchResolverTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly MyChildMatchResolver _resolver;

        public MyChildMatchResolverTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _resolver = new MyChildMatchResolver(_dbContext, _matchModule, _teamModule, _tournamentModule);
        }

        [Fact]
        public async Task ResolveAsync_ShouldReturnEmpty_WhenPlayerHasNoTournamentRegistrations()
        {
            var playerId = Guid.NewGuid();
            _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns([]);

            var result = await _resolver.ResolveAsync(playerId, _ => true, CancellationToken.None);

            result.Should().BeEmpty();
            await _matchModule.DidNotReceive().GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ResolveAsync_ShouldResolveTeamAndTournamentNames_AndOrderByScheduledAt()
        {
            var playerId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var awayTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var teamId = Guid.NewGuid();

            var tp = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns([tp]);

            var laterMatch = new TournamentMatchDto(Guid.NewGuid(), tournamentId, "Scheduled", DateTimeOffset.UtcNow.AddDays(5), tournamentTeamId, awayTeamId, 0, 0);
            var earlierMatch = new TournamentMatchDto(Guid.NewGuid(), tournamentId, "Finished", DateTimeOffset.UtcNow.AddDays(-5), tournamentTeamId, awayTeamId, 2, 1);
            _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns([laterMatch, earlierMatch]);

            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, teamId, Guid.NewGuid(), null, "Accepted"));
            _teamModule.GetTeamAsync(teamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(teamId, "Lions", "LIO"));
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 8));

            var result = await _resolver.ResolveAsync(playerId, _ => true, CancellationToken.None);

            result.Should().HaveCount(2);
            result[0].MatchId.Should().Be(earlierMatch.Id); // ordered by ScheduledAt ascending
            result[0].TournamentName.Should().Be("Cup");
            result[0].HomeTeamName.Should().Be("Lions");
            // away team lookup misses (not stubbed) -> falls back to "Unknown"
            result[0].AwayTeamName.Should().Be("Unknown");
        }

        [Fact]
        public async Task ResolveAsync_ShouldApplyFilter()
        {
            var playerId = Guid.NewGuid();
            var tournamentTeamId = Guid.NewGuid();
            var tp = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns([tp]);

            var scheduled = new TournamentMatchDto(Guid.NewGuid(), Guid.NewGuid(), "Scheduled", DateTimeOffset.UtcNow.AddDays(1), tournamentTeamId, Guid.NewGuid(), 0, 0);
            var finished = new TournamentMatchDto(Guid.NewGuid(), Guid.NewGuid(), "Finished", DateTimeOffset.UtcNow.AddDays(-1), tournamentTeamId, Guid.NewGuid(), 1, 0);
            _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns([scheduled, finished]);

            var result = await _resolver.ResolveAsync(playerId, m => m.Status == "Finished", CancellationToken.None);

            result.Should().ContainSingle();
            result[0].MatchId.Should().Be(finished.Id);
        }
    }
}
