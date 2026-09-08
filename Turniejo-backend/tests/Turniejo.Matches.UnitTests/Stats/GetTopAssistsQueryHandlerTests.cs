using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Stats.GetTopAssists;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Stats
{
    public sealed class GetTopAssistsQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IPlayerModule _playerModule;
        private readonly GetTopAssistsQueryHandler _handler;

        public GetTopAssistsQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _playerModule = Substitute.For<IPlayerModule>();
            _handler = new GetTopAssistsQueryHandler(_dbContext, _playerModule);
        }

        private static MatchEvent CreateEvent(MatchEventType type, Guid? playerId, Guid? relatedPlayerId = null) =>
            MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), type, 10, Guid.NewGuid(), Guid.NewGuid(), playerId, relatedPlayerId).Value;

        [Fact]
        public async Task Handle_ShouldCountBothDedicatedAssistEvents_AndGoalRelatedPlayer()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var assisterId = Guid.NewGuid();
            var scorerId = Guid.NewGuid();

            var events = new List<MatchEvent>
            {
                // Dedicated Assist event: PlayerId = assister.
                CreateEvent(MatchEventType.Assist, assisterId, scorerId),
                // Goal recorded with AssistPlayerId as RelatedPlayerId.
                CreateEvent(MatchEventType.Goal, scorerId, assisterId),
                // Penalty goal with no assist recorded.
                CreateEvent(MatchEventType.PenaltyGoal, scorerId, null),
            };
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(events);
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto> { new(assisterId, "Adam", "Nowicki") });

            // Act
            var result = await _handler.Handle(new GetTopAssistsQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].PlayerId.Should().Be(assisterId);
            result.Value[0].Count.Should().Be(2);
        }

        [Fact]
        public async Task Handle_ShouldNotCountOwnGoalRelatedPlayer()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var relatedPlayerId = Guid.NewGuid();
            var events = new List<MatchEvent>
            {
                CreateEvent(MatchEventType.OwnGoal, Guid.NewGuid(), relatedPlayerId),
            };
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns(events);
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerDto>());

            // Act
            var result = await _handler.Handle(new GetTopAssistsQuery(tournamentId), CancellationToken.None);

            // Assert: the handler only requests Assist/Goal/PenaltyGoal event types from the repository,
            // so an OwnGoal (even if one were returned) contributes nothing.
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
