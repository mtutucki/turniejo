using FluentAssertions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Matches.UnitTests.Entities
{
    public sealed class MatchTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidTournamentId => Guid.NewGuid();
        private static Guid ValidHomeTeamId => Guid.NewGuid();
        private static Guid ValidAwayTeamId => Guid.NewGuid();
        private static Guid ValidUserId => Guid.NewGuid();
        private static DateTimeOffset ValidScheduledAt => DateTimeOffset.UtcNow.AddDays(1);

        private static Match CreateValidMatch()
        {
            var match = Match.Create(
                ValidId, ValidTournamentId, Guid.NewGuid(), Guid.NewGuid(),
                ValidHomeTeamId, ValidAwayTeamId, Guid.NewGuid(),
                ValidScheduledAt, StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var homeTeamId = ValidHomeTeamId;
            var awayTeamId = ValidAwayTeamId;
            var scheduledAt = ValidScheduledAt;

            // Act
            var result = Match.Create(ValidId, ValidTournamentId, Guid.NewGuid(), Guid.NewGuid(), homeTeamId, awayTeamId, Guid.NewGuid(), scheduledAt, StageType.Group);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeTournamentTeamId.Should().Be(homeTeamId);
            result.Value.AwayTournamentTeamId.Should().Be(awayTeamId);
            result.Value.ScheduledAt.Should().Be(scheduledAt);
            result.Value.Status.Should().Be(MatchStatus.Scheduled);
            result.Value.ResultType.Should().Be(MatchResultType.NotPlayed);
            result.Value.StageType.Should().Be(StageType.Group);
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            var result = Match.Create(Guid.Empty, ValidTournamentId, null, null, ValidHomeTeamId, ValidAwayTeamId, null, ValidScheduledAt, StageType.Group);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenTournamentIdIsEmpty()
        {
            var result = Match.Create(ValidId, Guid.Empty, null, null, ValidHomeTeamId, ValidAwayTeamId, null, ValidScheduledAt, StageType.Group);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentId");
        }

        [Fact]
        public void Create_ShouldFail_WhenHomeTeamIsEmpty()
        {
            var result = Match.Create(ValidId, ValidTournamentId, null, null, Guid.Empty, ValidAwayTeamId, null, ValidScheduledAt, StageType.Group);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.HomeTournamentTeamId");
        }

        [Fact]
        public void Create_ShouldFail_WhenAwayTeamIsEmpty()
        {
            var result = Match.Create(ValidId, ValidTournamentId, null, null, ValidHomeTeamId, Guid.Empty, null, ValidScheduledAt, StageType.Group);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.AwayTournamentTeamId");
        }

        [Fact]
        public void Create_ShouldFail_WhenHomeAndAwayTeamAreTheSame()
        {
            var teamId = Guid.NewGuid();
            var result = Match.Create(ValidId, ValidTournamentId, null, null, teamId, teamId, null, ValidScheduledAt, StageType.Group);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.AwayTournamentTeamId");
        }

        [Fact]
        public void UpdateSchedule_ShouldUpdatePitchAndTime_WhenValid()
        {
            // Arrange
            var match = CreateValidMatch();
            var newPitchId = Guid.NewGuid();
            var newTime = ValidScheduledAt.AddHours(2);

            // Act
            var result = match.UpdateSchedule(newPitchId, newTime, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.PitchId.Should().Be(newPitchId);
            match.ScheduledAt.Should().Be(newTime);
            match.DomainEvents.Should().ContainSingle(e => e is MatchScheduleChanged);
        }

        [Fact]
        public void AssignPitch_ShouldUpdatePitch_WhenValid()
        {
            // Arrange
            var match = CreateValidMatch();
            var newPitchId = Guid.NewGuid();

            // Act
            var result = match.AssignPitch(newPitchId, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.PitchId.Should().Be(newPitchId);
            match.DomainEvents.Should().ContainSingle(e => e is MatchPitchChanged);
        }

        [Fact]
        public void AssignPitch_ShouldAllowUnassigning()
        {
            // Arrange
            var match = CreateValidMatch();

            // Act
            var result = match.AssignPitch(null, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.PitchId.Should().BeNull();
        }

        [Fact]
        public void Reschedule_ShouldUpdateScheduledAt_WhenValid()
        {
            // Arrange
            var match = CreateValidMatch();
            var newTime = ValidScheduledAt.AddDays(3);

            // Act
            var result = match.Reschedule(newTime, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.ScheduledAt.Should().Be(newTime);
            match.DomainEvents.Should().ContainSingle(e => e is MatchTimeChanged);
        }

        [Fact]
        public void AssignReferee_ShouldSetRefereeId_WhenValid()
        {
            // Arrange
            var match = CreateValidMatch();
            var refereeId = Guid.NewGuid();

            // Act
            var result = match.AssignReferee(refereeId, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().Be(refereeId);
            match.DomainEvents.Should().ContainSingle(e => e is RefereeAssignedToMatch);
        }

        [Fact]
        public void AssignReferee_ShouldAllowUnassigning()
        {
            // Arrange
            var match = CreateValidMatch();
            match.AssignReferee(Guid.NewGuid(), ValidUserId);
            match.ClearDomainEvents();

            // Act
            var result = match.AssignReferee(null, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.RefereeId.Should().BeNull();
        }

        [Fact]
        public void Start_ShouldSetStatusLiveAndStartedAt_WhenScheduled()
        {
            // Arrange
            var match = CreateValidMatch();

            // Act
            var result = match.Start(ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Live);
            match.StartedAt.Should().NotBeNull();
            match.DomainEvents.Should().ContainSingle(e => e is MatchStarted);
        }

        [Fact]
        public void Start_ShouldFail_WhenNotScheduled()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.ClearDomainEvents();

            // Act
            var result = match.Start(ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void Pause_ShouldSetStatusPausedAndPausedAt_WhenLive()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.ClearDomainEvents();

            // Act
            var result = match.Pause(ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Paused);
            match.PausedAt.Should().NotBeNull();
            match.DomainEvents.Should().ContainSingle(e => e is MatchPaused);
        }

        [Fact]
        public void Pause_ShouldFail_WhenNotLive()
        {
            // Arrange
            var match = CreateValidMatch();

            // Act
            var result = match.Pause(ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void Resume_ShouldSetStatusLiveAndAccumulatePausedSeconds_WhenPaused()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Pause(ValidUserId);
            match.ClearDomainEvents();

            // Act
            var result = match.Resume(ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Live);
            match.PausedAt.Should().BeNull();
            match.TotalPausedSeconds.Should().BeGreaterThanOrEqualTo(0);
            match.DomainEvents.Should().ContainSingle(e => e is MatchResumed);
        }

        [Fact]
        public void Resume_ShouldFail_WhenNotPaused()
        {
            // Arrange
            var match = CreateValidMatch();

            // Act
            var result = match.Resume(ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void Finish_ShouldSetStatusFinishedAndEditLockedAt_WhenLive()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.ClearDomainEvents();
            var winnerId = match.HomeTournamentTeamId;

            // Act
            var result = match.Finish(MatchResultType.Regular, winnerId, 60, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Finished);
            match.FinishedAt.Should().NotBeNull();
            match.ResultType.Should().Be(MatchResultType.Regular);
            match.WinnerTournamentTeamId.Should().Be(winnerId);
            match.EditLockedAt.Should().Be(match.FinishedAt!.Value.AddMinutes(60));
            match.DomainEvents.Should().ContainSingle(e => e is MatchFinished);
        }

        [Fact]
        public void Finish_ShouldFail_WhenAlreadyFinished()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, null, 60, ValidUserId);
            match.ClearDomainEvents();

            // Act
            var result = match.Finish(MatchResultType.Regular, null, 60, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void Finish_ShouldFail_WhenResultTypeIsNotPlayed()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            // Act
            var result = match.Finish(MatchResultType.NotPlayed, null, 60, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ResultType");
        }

        [Fact]
        public void Finish_ShouldFail_WhenWinnerIsNotOneOfTheTeams()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            // Act
            var result = match.Finish(MatchResultType.Regular, Guid.NewGuid(), 60, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.WinnerTournamentTeamId");
        }

        [Fact]
        public void GetElapsedSeconds_ShouldReturnZero_WhenNotStarted()
        {
            var match = CreateValidMatch();
            match.GetElapsedSeconds(DateTimeOffset.UtcNow).Should().Be(0);
        }

        [Fact]
        public void GetElapsedSeconds_ShouldSubtractTotalPausedSeconds()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            var at = match.StartedAt!.Value.AddSeconds(100);

            // Act & Assert
            match.GetElapsedSeconds(at).Should().Be(100);
        }

        [Fact]
        public void EnsureEditable_ShouldSucceed_WhenLive()
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            match.EnsureEditable().IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void EnsureEditable_ShouldSucceed_WhenPaused()
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Pause(ValidUserId);

            match.EnsureEditable().IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void EnsureEditable_ShouldFail_WhenScheduled()
        {
            var match = CreateValidMatch();
            var result = match.EnsureEditable();
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void EnsureEditable_ShouldSucceed_WhenFinishedWithinEditWindow()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, null, 60, ValidUserId);

            // Act & Assert
            match.EnsureEditable().IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void EnsureEditable_ShouldFail_WhenFinishedOutsideEditWindow()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, null, -1, ValidUserId);

            // Act & Assert
            var result = match.EnsureEditable();
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void UpdateScore_ShouldSetScoresAndRaiseEvent_WhenChanged()
        {
            // Arrange
            var match = CreateValidMatch();

            // Act
            var result = match.UpdateScore(2, 1);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(2);
            match.AwayScore.Should().Be(1);
            match.DomainEvents.Should().ContainSingle(e => e is MatchScoreChanged);
        }

        [Fact]
        public void UpdateScore_ShouldNotRaiseEvent_WhenUnchanged()
        {
            // Arrange
            var match = CreateValidMatch();
            match.UpdateScore(1, 0);
            match.ClearDomainEvents();

            // Act
            var result = match.UpdateScore(1, 0);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void UpdateScore_ShouldFail_WhenNegative()
        {
            var match = CreateValidMatch();
            var result = match.UpdateScore(-1, 0);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Score");
        }

        [Fact]
        public void SetManualScore_ShouldSetScoreAndResultType_WhenNotFinished()
        {
            // Arrange
            var match = CreateValidMatch();

            // Act
            var result = match.SetManualScore(4, 1, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(4);
            match.AwayScore.Should().Be(1);
            match.ResultType.Should().Be(MatchResultType.ManualDecision);
            match.DomainEvents.Should().ContainSingle(e => e is MatchScoreChanged);
        }

        [Fact]
        public void SetManualScore_ShouldFail_WhenMatchIsFinished()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, null, 60, ValidUserId);

            // Act
            var result = match.SetManualScore(2, 0, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void CorrectFinishedScore_ShouldUpdateScore_WhenWithinEditWindow()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, null, 60, ValidUserId);
            match.ClearDomainEvents();

            // Act
            var result = match.CorrectFinishedScore(3, 2, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(3);
            match.AwayScore.Should().Be(2);
            match.DomainEvents.Should().ContainSingle(e => e is MatchScoreCorrected);
        }

        [Fact]
        public void CorrectFinishedScore_ShouldFail_WhenNotFinished()
        {
            var match = CreateValidMatch();
            var result = match.CorrectFinishedScore(1, 1, ValidUserId);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void CorrectFinishedScore_ShouldFail_WhenOutsideEditWindow()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, null, -1, ValidUserId);

            // Act
            var result = match.CorrectFinishedScore(1, 1, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.EditLockedAt");
        }
    }
}
