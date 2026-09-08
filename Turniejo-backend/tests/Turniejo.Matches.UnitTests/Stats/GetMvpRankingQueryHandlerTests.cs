using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Stats.GetMvpRanking;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Stats
{
    public sealed class GetMvpRankingQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IPlayerModule _playerModule;
        private readonly GetMvpRankingQueryHandler _handler;

        public GetMvpRankingQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _playerModule = Substitute.For<IPlayerModule>();
            _handler = new GetMvpRankingQueryHandler(_dbContext, _playerModule);
        }

        private static MatchEvent CreateEvent(MatchEventType type, Guid? playerId) =>
            MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), type, 0, Guid.NewGuid(), Guid.NewGuid(), playerId).Value;

        [Fact]
        public async Task Handle_ShouldCountMvpSelectionsAcrossMatches_GroupedByPlayer()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var mvpPlayerId = Guid.NewGuid();
            var otherPlayerId = Guid.NewGuid();

            var events = new List<MatchEvent>
            {
                CreateEvent(MatchEventType.MvpSelected, mvpPlayerId),
                CreateEvent(MatchEventType.MvpSelected, mvpPlayerId),
                CreateEvent(MatchEventType.MvpSelected, otherPlayerId),
            };
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(events);
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto>
                {
                    new(mvpPlayerId, "Marek", "Wojcik"),
                    new(otherPlayerId, "Tomasz", "Lis"),
                });

            // Act
            var result = await _handler.Handle(new GetMvpRankingQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].PlayerId.Should().Be(mvpPlayerId);
            result.Value[0].Count.Should().Be(2);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenNoMvpEvents()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(new List<MatchEvent>());
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto>());

            // Act
            var result = await _handler.Handle(new GetMvpRankingQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
