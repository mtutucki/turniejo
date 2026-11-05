using FluentAssertions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Matches.UnitTests.Entities
{
    public sealed class MatchChangeFinishedResultTests
    {
        private static readonly Guid UserId = Guid.NewGuid();

        private static Match CreateMatch(StageType stage)
        {
            var match = Match.Create(
                Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null,
                DateTimeOffset.UtcNow.AddDays(1), stage).Value;
            match.Start(UserId);
            match.ClearDomainEvents();
            return match;
        }

        private static Match FinishedKnockout(int home, int away, int editWindowMinutes = 60)
        {
            var match = CreateMatch(StageType.Knockout);
            match.UpdateScore(home, away);
            match.Finish(MatchResultType.Regular, null, editWindowMinutes, UserId);
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public void ChangeFinishedResult_ShouldSetWalkover_AndPublishMatchScoreCorrected()
        {
            var match = FinishedKnockout(2, 1);

            var result = match.ChangeFinishedResult(
                MatchResultType.Walkover, match.AwayTournamentTeamId, 2, 1, false, UserId,
                walkoverWinnerScore: 3, walkoverLoserScore: 0);

            result.IsSuccess.Should().BeTrue();
            match.ResultType.Should().Be(MatchResultType.Walkover);
            match.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
            match.HomeScore.Should().Be(0);
            match.AwayScore.Should().Be(3);
            match.DomainEvents.Should().ContainSingle(e => e is MatchScoreCorrected);
        }

        [Fact]
        public void ChangeFinishedResult_ShouldSetPenalties_WhenGoalsAreLevel()
        {
            // Mecz pucharowy zakończony 1:0, potem sędzia cofnął gola: 1:1 trzeba rozstrzygnąć karnymi.
            var match = FinishedKnockout(1, 0);

            var result = match.ChangeFinishedResult(
                MatchResultType.Penalties, null, 1, 1, false, UserId,
                homePenaltyScore: 3, awayPenaltyScore: 4);

            result.IsSuccess.Should().BeTrue();
            match.ResultType.Should().Be(MatchResultType.Penalties);
            match.HomeScore.Should().Be(1);
            match.AwayScore.Should().Be(1);
            match.HomePenaltyScore.Should().Be(3);
            match.AwayPenaltyScore.Should().Be(4);
            match.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
        }

        [Fact]
        public void ChangeFinishedResult_ShouldRestoreGoalScore_WhenWalkoverIsChangedBack()
        {
            var match = FinishedKnockout(2, 1);
            match.ChangeFinishedResult(
                MatchResultType.Walkover, match.AwayTournamentTeamId, 2, 1, false, UserId,
                walkoverWinnerScore: 3, walkoverLoserScore: 0);

            var result = match.ChangeFinishedResult(MatchResultType.Regular, null, 2, 1, false, UserId);

            result.IsSuccess.Should().BeTrue();
            match.ResultType.Should().Be(MatchResultType.Regular);
            match.HomeScore.Should().Be(2);
            match.AwayScore.Should().Be(1);
            match.WinnerTournamentTeamId.Should().Be(match.HomeTournamentTeamId);
        }

        [Fact]
        public void ChangeFinishedResult_ShouldClearPenalties_WhenNoLongerDecidedByPenalties()
        {
            var match = FinishedKnockout(1, 0);
            match.ChangeFinishedResult(MatchResultType.Penalties, null, 1, 1, false, UserId, homePenaltyScore: 5, awayPenaltyScore: 4);

            match.ChangeFinishedResult(
                MatchResultType.Walkover, match.AwayTournamentTeamId, 1, 1, false, UserId,
                walkoverWinnerScore: 3, walkoverLoserScore: 0);

            match.HomePenaltyScore.Should().BeNull();
            match.AwayPenaltyScore.Should().BeNull();
        }

        [Fact]
        public void ChangeFinishedResult_ShouldFail_WhenKnockoutDrawIsNotDecided()
        {
            var match = FinishedKnockout(1, 0);

            var result = match.ChangeFinishedResult(MatchResultType.Regular, null, 1, 1, false, UserId);

            result.IsFailure.Should().BeTrue();
            match.ResultType.Should().Be(MatchResultType.Regular);
            match.HomeScore.Should().Be(1);
            match.AwayScore.Should().Be(0);
        }

        [Fact]
        public void ChangeFinishedResult_ShouldFail_AfterEditWindow_UnlessIgnored()
        {
            var match = FinishedKnockout(2, 1, editWindowMinutes: -1);

            var asReferee = match.ChangeFinishedResult(
                MatchResultType.Walkover, match.AwayTournamentTeamId, 2, 1, false, UserId,
                walkoverWinnerScore: 3, walkoverLoserScore: 0);
            var asOrganizer = match.ChangeFinishedResult(
                MatchResultType.Walkover, match.AwayTournamentTeamId, 2, 1, true, UserId,
                walkoverWinnerScore: 3, walkoverLoserScore: 0);

            asReferee.IsFailure.Should().BeTrue();
            asReferee.Error.Code.Should().Be("Conflict.EditLockedAt");
            asOrganizer.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void ChangeFinishedResult_ShouldFail_WhenMatchIsNotFinished()
        {
            var match = CreateMatch(StageType.Group);

            var result = match.ChangeFinishedResult(MatchResultType.Regular, null, 0, 0, true, UserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void UpdateScore_ShouldKeepWalkoverScore_WhenGoalsAreCorrectedAfterwards()
        {
            var match = CreateMatch(StageType.Group);
            match.Finish(MatchResultType.Walkover, match.HomeTournamentTeamId, 60, UserId, walkoverWinnerScore: 3, walkoverLoserScore: 0);

            match.UpdateScore(1, 1);

            match.HomeScore.Should().Be(3);
            match.AwayScore.Should().Be(0);
        }
    }
}
