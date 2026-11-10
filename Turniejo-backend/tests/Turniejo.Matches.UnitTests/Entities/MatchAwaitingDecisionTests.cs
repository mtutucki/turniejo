using FluentAssertions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Matches.UnitTests.Entities
{
    /// <summary>Porzucony remis w pucharze: system raz go wstrzymuje i prosi organizatora o rozstrzygnięcie.</summary>
    public sealed class MatchAwaitingDecisionTests
    {
        private static Match LiveMatch(StageType stage = StageType.Knockout)
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, stage).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Theory]
        [InlineData(StageType.Knockout)]
        [InlineData(StageType.ThirdPlace)]
        [InlineData(StageType.Final)]
        public void NeedsDecisionToFinish_ShouldBeTrue_ForADrawOutsideTheGroupStage(StageType stage)
        {
            LiveMatch(stage).NeedsDecisionToFinish.Should().BeTrue();
        }

        [Fact]
        public void NeedsDecisionToFinish_ShouldBeFalse_ForAGroupDraw_OrAKnockoutMatchWithALeader()
        {
            LiveMatch(StageType.Group).NeedsDecisionToFinish.Should().BeFalse();

            var leader = LiveMatch();
            leader.SetManualScore(1, 0, Guid.NewGuid());
            leader.NeedsDecisionToFinish.Should().BeFalse();
        }

        [Fact]
        public void MarkAwaitingDecision_ShouldPauseALiveMatch_AndRaiseTheEvent()
        {
            var match = LiveMatch();
            var now = DateTimeOffset.UtcNow;

            var result = match.MarkAwaitingDecision(now);

            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Paused);
            match.AwaitingDecisionSince.Should().Be(now);
            match.DomainEvents.Should().ContainSingle(e => e is MatchPaused);
            match.DomainEvents.OfType<MatchAwaitingDecision>().Should().ContainSingle()
                .Which.Should().Match<MatchAwaitingDecision>(e =>
                    e.MatchId == match.Id && e.TournamentId == match.TournamentId &&
                    e.HomeTournamentTeamId == match.HomeTournamentTeamId && e.AwayTournamentTeamId == match.AwayTournamentTeamId);
        }

        [Fact]
        public void MarkAwaitingDecision_ShouldKeepAnAlreadyPausedMatchPaused_AndStillRaiseTheEvent()
        {
            var match = LiveMatch();
            match.Pause(Guid.NewGuid());
            match.ClearDomainEvents();

            match.MarkAwaitingDecision(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

            match.Status.Should().Be(MatchStatus.Paused);
            match.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MatchAwaitingDecision>();
        }

        [Fact]
        public void MarkAwaitingDecision_ShouldHappenOnlyOnce_EvenAfterTheOrganizerResumes()
        {
            var match = LiveMatch();
            match.MarkAwaitingDecision(DateTimeOffset.UtcNow);
            match.Resume(Guid.NewGuid());
            match.ClearDomainEvents();

            var again = match.MarkAwaitingDecision(DateTimeOffset.UtcNow);

            again.IsFailure.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Live);
            match.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void MarkAwaitingDecision_ShouldFail_ForAMatchThatDoesNotNeedADecision()
        {
            var match = LiveMatch(StageType.Group);

            match.MarkAwaitingDecision(DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
            match.AwaitingDecisionSince.Should().BeNull();
        }

        [Fact]
        public void MarkAwaitingDecision_ShouldFail_ForAFinishedMatch()
        {
            var match = LiveMatch();
            match.SetManualScore(1, 0, Guid.NewGuid());
            match.Finish(MatchResultType.Regular, null, 60, Guid.NewGuid());

            match.MarkAwaitingDecision(DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
        }

        [Fact]
        public void IsAwaitingDecision_ShouldBeTrue_WhileTheSystemPauseLasts()
        {
            var live = LiveMatch();
            live.MarkAwaitingDecision(DateTimeOffset.UtcNow);
            live.IsAwaitingDecision.Should().BeTrue();
            live.PausedAt.Should().Be(live.AwaitingDecisionSince);

            var paused = LiveMatch();
            paused.Pause(Guid.NewGuid());
            paused.MarkAwaitingDecision(DateTimeOffset.UtcNow);
            paused.IsAwaitingDecision.Should().BeTrue();
        }

        [Fact]
        public void IsAwaitingDecision_ShouldBeFalse_OnceTheOrganizerResumes_EvenIfTheyPauseAgain()
        {
            var match = LiveMatch();
            match.MarkAwaitingDecision(DateTimeOffset.UtcNow);

            match.Resume(Guid.NewGuid());
            match.IsAwaitingDecision.Should().BeFalse();

            Thread.Sleep(5);
            match.Pause(Guid.NewGuid());
            match.IsAwaitingDecision.Should().BeFalse();
        }

        [Fact]
        public void IsAwaitingDecision_ShouldBeFalse_ForAnOrdinaryPause()
        {
            var match = LiveMatch();
            match.Pause(Guid.NewGuid());

            match.IsAwaitingDecision.Should().BeFalse();
        }
    }
}
