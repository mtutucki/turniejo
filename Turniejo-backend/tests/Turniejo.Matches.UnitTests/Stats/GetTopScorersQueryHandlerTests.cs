using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Stats.GetTopScorers;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Stats
{
    public sealed class GetTopScorersQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IPlayerModule _playerModule;
        private readonly GetTopScorersQueryHandler _handler;

        public GetTopScorersQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _playerModule = Substitute.For<IPlayerModule>();
            _handler = new GetTopScorersQueryHandler(_dbContext, _playerModule);
        }

        private static MatchEvent CreateEvent(MatchEventType type, Guid? playerId, Guid? relatedPlayerId = null) =>
            MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), type, 10, Guid.NewGuid(), Guid.NewGuid(), playerId, relatedPlayerId).Value;

        [Fact]
        public async Task Handle_ShouldCountGoalsAndPenalties_GroupedByPlayer_OrderedDescending()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var topScorerId = Guid.NewGuid();
            var secondScorerId = Guid.NewGuid();

            var events = new List<MatchEvent>
            {
                CreateEvent(MatchEventType.Goal, topScorerId),
                CreateEvent(MatchEventType.PenaltyGoal, topScorerId),
                CreateEvent(MatchEventType.Goal, secondScorerId),
            };
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(events);

            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto>
                {
                    new(topScorerId, "Jan", "Kowalski"),
                    new(secondScorerId, "Piotr", "Nowak"),
                });

            // Act
            var result = await _handler.Handle(new GetTopScorersQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].PlayerId.Should().Be(topScorerId);
            result.Value[0].Count.Should().Be(2);
            result.Value[0].FirstName.Should().Be("Jan");
            result.Value[1].PlayerId.Should().Be(secondScorerId);
            result.Value[1].Count.Should().Be(1);
        }

        [Fact]
        public async Task Handle_ShouldNotRequestOwnGoal_AsAScorerType()
        {
            // Zamyka lukę z audytu: samobój nie może zasilać rankingu strzelców jego sprawcy —
            // filtr typów przekazany do zapytania nie może już zawierać OwnGoal.
            var tournamentId = Guid.NewGuid();
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(new List<MatchEvent>());
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto>());

            // Act
            await _handler.Handle(new GetTopScorersQuery(tournamentId), CancellationToken.None);

            // Assert
            await _dbContext.Received(1).GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId,
                Arg.Is<IReadOnlyCollection<MatchEventType>>(types => !types.Contains(MatchEventType.OwnGoal)),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldLimitResults_ToTopParameter()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var events = Enumerable.Range(0, 5)
                .Select(_ => CreateEvent(MatchEventType.Goal, Guid.NewGuid()))
                .ToList();
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(events);
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto>());

            // Act
            var result = await _handler.Handle(new GetTopScorersQuery(tournamentId, Top: 2), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenNoGoalEvents()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(new List<MatchEvent>());
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto>());

            // Act
            var result = await _handler.Handle(new GetTopScorersQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
