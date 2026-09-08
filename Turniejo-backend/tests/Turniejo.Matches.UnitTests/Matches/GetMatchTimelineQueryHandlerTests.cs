using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchTimeline;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchTimelineQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IPlayerModule _playerModule;
        private readonly GetMatchTimelineQueryHandler _handler;

        public GetMatchTimelineQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _playerModule = Substitute.For<IPlayerModule>();
            _handler = new GetMatchTimelineQueryHandler(_dbContext, _teamModule, _playerModule);

            _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new List<PlayerDto>());
        }

        [Fact]
        public async Task Handle_ShouldResolveTeamAndPlayerNames_AndOrderByMinute()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var homeTeamId = Guid.NewGuid();
            var scorerId = Guid.NewGuid();
            var assistId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;

            _teamModule.GetTournamentTeamAsync(match.HomeTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(match.HomeTournamentTeamId, tournamentId, homeTeamId, Guid.NewGuid(), null, "Accepted"));
            _teamModule.GetTeamAsync(homeTeamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(homeTeamId, "Lions", "LIO"));
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto> { new(scorerId, "Jan", "Kowalski"), new(assistId, "Piotr", "Nowak") });

            var lateGoal = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 60, Guid.NewGuid(), match.HomeTournamentTeamId, scorerId, assistId).Value;
            var earlyGoal = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId, scorerId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns([lateGoal, earlyGoal]);

            // Act
            var result = await _handler.Handle(new GetMatchTimelineQuery(tournamentId, match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].Minute.Should().Be(10);
            result.Value[0].TeamName.Should().Be("Lions");
            result.Value[0].PlayerName.Should().Be("Jan Kowalski");
            result.Value[1].Minute.Should().Be(60);
            result.Value[1].RelatedPlayerName.Should().Be("Piotr Nowak");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchTimelineQuery(Guid.NewGuid(), matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
