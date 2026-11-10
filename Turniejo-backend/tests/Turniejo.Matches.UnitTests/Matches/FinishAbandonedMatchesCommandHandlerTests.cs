using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.FinishAbandonedMatches;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class FinishAbandonedMatchesCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly FinishAbandonedMatchesCommandHandler _handler;

        public FinishAbandonedMatchesCommandHandlerTests()
        {
            _handler = new FinishAbandonedMatchesCommandHandler(_dbContext, _tournamentModule);
        }

        private static Match LiveMatch(Guid tournamentId, StageType stage = StageType.Group)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, stage).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        private void GivenSettings(Guid tournamentId, int matchMinutes) =>
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, matchMinutes, 5, 60, 3, 1, 0, false, false, 3, 0));

        private void GivenCandidates(params Match[] matches) =>
            _dbContext.GetTrackedUnfinishedMatchesStartedBeforeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns(matches);

        private Task<int> RunAsync(DateTimeOffset now) =>
            _handler.Handle(new FinishAbandonedMatchesCommand(now), CancellationToken.None)
                .ContinueWith(t => t.Result.Value.FinishedCount);

        [Fact]
        public async Task Handle_ShouldAskOnlyForMatchesStartedBeforeTheMinimumThreshold()
        {
            var now = DateTimeOffset.UtcNow;
            GivenCandidates();

            await RunAsync(now);

            await _dbContext.Received(1).GetTrackedUnfinishedMatchesStartedBeforeAsync(now - TimeSpan.FromHours(2), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldKeepTheMatch_ExactlyAtTheThreshold_AndFinishIt_JustAfter()
        {
            var tournamentId = Guid.NewGuid();
            GivenSettings(tournamentId, matchMinutes: 60);
            var match = LiveMatch(tournamentId);
            GivenCandidates(match);
            var threshold = match.StartedAt!.Value.AddHours(3);

            (await RunAsync(threshold.AddSeconds(-1))).Should().Be(0);
            match.Status.Should().Be(MatchStatus.Live);

            (await RunAsync(threshold)).Should().Be(1);
            match.Status.Should().Be(MatchStatus.Finished);
        }

        [Fact]
        public async Task Handle_ShouldUseTwoHours_WhenThreeTimesTheDurationIsShorter()
        {
            var tournamentId = Guid.NewGuid();
            GivenSettings(tournamentId, matchMinutes: 20);
            var match = LiveMatch(tournamentId);
            GivenCandidates(match);

            (await RunAsync(match.StartedAt!.Value.AddMinutes(119))).Should().Be(0);
            (await RunAsync(match.StartedAt!.Value.AddMinutes(120))).Should().Be(1);
        }

        [Fact]
        public async Task Handle_ShouldFinishAPausedMatch_CountingTheTimeFromKickOff()
        {
            var tournamentId = Guid.NewGuid();
            GivenSettings(tournamentId, matchMinutes: 30);
            var match = LiveMatch(tournamentId);
            match.Pause(Guid.NewGuid());
            match.ClearDomainEvents();
            GivenCandidates(match);

            (await RunAsync(match.StartedAt!.Value.AddHours(2))).Should().Be(1);

            match.Status.Should().Be(MatchStatus.Finished);
            match.ResultType.Should().Be(MatchResultType.Regular);
        }

        [Fact]
        public async Task Handle_ShouldApplyEachTournamentsOwnDuration()
        {
            var shortTournament = Guid.NewGuid();
            var longTournament = Guid.NewGuid();
            GivenSettings(shortTournament, matchMinutes: 30);
            GivenSettings(longTournament, matchMinutes: 90);
            var shortMatch = LiveMatch(shortTournament);
            var longMatch = LiveMatch(longTournament);
            GivenCandidates(shortMatch, longMatch);

            // 2,5 h: krótki (próg 2 h) po czasie, długi (próg 4,5 h) jeszcze trwa.
            (await RunAsync(shortMatch.StartedAt!.Value.AddHours(2.5))).Should().Be(1);

            shortMatch.Status.Should().Be(MatchStatus.Finished);
            longMatch.Status.Should().Be(MatchStatus.Live);
        }

        [Fact]
        public async Task Handle_ShouldSkipATournamentWithoutSettings_AndStillFinishTheOthers()
        {
            var missing = Guid.NewGuid();
            var present = Guid.NewGuid();
            _tournamentModule.GetTournamentSettingsAsync(missing, Arg.Any<CancellationToken>()).Returns((TournamentSettingsSummaryDto?)null);
            GivenSettings(present, matchMinutes: 30);
            var orphan = LiveMatch(missing);
            var match = LiveMatch(present);
            GivenCandidates(orphan, match);

            (await RunAsync(match.StartedAt!.Value.AddHours(3))).Should().Be(1);

            orphan.Status.Should().Be(MatchStatus.Live);
            match.Status.Should().Be(MatchStatus.Finished);
        }

        [Fact]
        public async Task Handle_ShouldPauseAKnockoutDrawForTheOrganizer_ButFinishAKnockoutMatchWithALeader()
        {
            var tournamentId = Guid.NewGuid();
            GivenSettings(tournamentId, matchMinutes: 30);
            var draw = LiveMatch(tournamentId, StageType.Knockout);
            var leader = LiveMatch(tournamentId, StageType.Knockout);
            leader.SetManualScore(1, 0, Guid.NewGuid());
            leader.ClearDomainEvents();
            GivenCandidates(draw, leader);
            var now = leader.StartedAt!.Value.AddHours(3);

            var result = await _handler.Handle(new FinishAbandonedMatchesCommand(now), CancellationToken.None);

            result.Value.Should().Be(new FinishAbandonedMatchesResponse(FinishedCount: 1, AwaitingDecisionCount: 1));
            draw.Status.Should().Be(MatchStatus.Paused);
            draw.AwaitingDecisionSince.Should().Be(now);
            leader.Status.Should().Be(MatchStatus.Finished);
            leader.WinnerTournamentTeamId.Should().Be(leader.HomeTournamentTeamId);
            // Pauza + prośba o rozstrzygnięcie (draw), koniec meczu (leader).
            await _dbContext.Received(3).AddOutboxMessageAsync(Arg.Any<Turniejo.SharedKernel.Outbox.OutboxMessage>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotTouchAMatchAlreadyHandedToTheOrganizer_EvenWhenItNowHasALeader()
        {
            var tournamentId = Guid.NewGuid();
            GivenSettings(tournamentId, matchMinutes: 30);
            var match = LiveMatch(tournamentId, StageType.Knockout);
            match.MarkAwaitingDecision(DateTimeOffset.UtcNow);
            match.Resume(Guid.NewGuid());
            match.SetManualScore(1, 0, Guid.NewGuid()); // gol w dogrywce po wznowieniu
            match.ClearDomainEvents();
            GivenCandidates(match);

            (await RunAsync(match.StartedAt!.Value.AddHours(5))).Should().Be(0);

            match.Status.Should().Be(MatchStatus.Live);
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldWriteTheFinishEventAndOutbox_AndSaveOnce()
        {
            var tournamentId = Guid.NewGuid();
            GivenSettings(tournamentId, matchMinutes: 30);
            var first = LiveMatch(tournamentId);
            var second = LiveMatch(tournamentId);
            GivenCandidates(first, second);

            (await RunAsync(first.StartedAt!.Value.AddHours(3))).Should().Be(2);

            await _dbContext.Received(2).AddMatchEventAsync(
                Arg.Is<MatchEvent>(e => e.Type == MatchEventType.MatchFinished && e.CreatedByUserId == Guid.Empty),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(2).AddOutboxMessageAsync(Arg.Any<Turniejo.SharedKernel.Outbox.OutboxMessage>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotSave_WhenNothingWasFinished()
        {
            GivenCandidates();

            (await RunAsync(DateTimeOffset.UtcNow)).Should().Be(0);

            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
