using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecordMatchResult;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class RecordMatchResultCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
        private readonly RecordMatchResultCommandHandler _handler;
        private readonly List<MatchEvent> _addedEvents = [];

        public RecordMatchResultCommandHandlerTests()
        {
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            _dbContext.AddMatchEventAsync(Arg.Do<MatchEvent>(_addedEvents.Add), Arg.Any<CancellationToken>());
            _handler = new RecordMatchResultCommandHandler(_dbContext, _tournamentModule, _currentUserContext);
        }

        private Match ArrangeScheduledMatch(StageType stage = StageType.Group)
        {
            var tournamentId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), stage).Value;
            match.ClearDomainEvents();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));
            return match;
        }

        [Fact]
        public async Task Handle_ShouldFinishMatch_WithGoalsWithoutScorers()
        {
            var match = ArrangeScheduledMatch();
            var command = new RecordMatchResultCommand(match.TournamentId, match.Id, 3, 1, MatchResultType.Regular, null);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Finished);
            match.HomeScore.Should().Be(3);
            match.AwayScore.Should().Be(1);
            match.EditLockedAt.Should().BeAfter(DateTimeOffset.UtcNow);
            _addedEvents.Where(e => e.Type == MatchEventType.Goal).Should().HaveCount(4)
                .And.OnlyContain(e => e.PlayerId == null);
            _addedEvents.Count(e => e.Type == MatchEventType.Goal && e.TournamentTeamId == match.HomeTournamentTeamId).Should().Be(3);
            _addedEvents.Should().ContainSingle(e => e.Type == MatchEventType.MatchFinished);
            await _dbContext.Received().AddOutboxMessageAsync(
                Arg.Is<OutboxMessage>(m => m.Type.Contains("MatchFinished")), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().AddOutboxMessageAsync(
                Arg.Is<OutboxMessage>(m => m.Type.Contains("MatchStarted")), Arg.Any<CancellationToken>());
            await _tournamentModule.Received(1).MarkTournamentStartedAsync(match.TournamentId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDecideKnockoutDrawOnPenalties()
        {
            var match = ArrangeScheduledMatch(StageType.Knockout);
            var command = new RecordMatchResultCommand(
                match.TournamentId, match.Id, 2, 2, MatchResultType.Penalties, null, HomePenaltyScore: 4, AwayPenaltyScore: 3);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.WinnerTournamentTeamId.Should().Be(match.HomeTournamentTeamId);
            match.HomePenaltyScore.Should().Be(4);
        }

        [Fact]
        public async Task Handle_ShouldRejectKnockoutDrawWithoutPenalties()
        {
            var match = ArrangeScheduledMatch(StageType.Knockout);
            var command = new RecordMatchResultCommand(match.TournamentId, match.Id, 1, 1, MatchResultType.Regular, null);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Scheduled);
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldUseTournamentScores_ForWalkover_AndSkipGoals()
        {
            var match = ArrangeScheduledMatch();
            var command = new RecordMatchResultCommand(match.TournamentId, match.Id, 0, 0, MatchResultType.Walkover, match.AwayTournamentTeamId);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(0);
            match.AwayScore.Should().Be(3);
            _addedEvents.Should().NotContain(e => e.Type == MatchEventType.Goal);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchAlreadyStarted()
        {
            var match = ArrangeScheduledMatch();
            match.Start(Guid.NewGuid());
            var command = new RecordMatchResultCommand(match.TournamentId, match.Id, 1, 0, MatchResultType.Regular, null);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            _addedEvents.Should().BeEmpty();
        }
    }
}
