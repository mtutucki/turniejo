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
        public void ChangeTeams_ShouldReplaceTeams_AndKeepSchedule_BeforeTheMatchStarts()
        {
            // Arrange
            var match = CreateValidMatch();
            var pitchBefore = match.PitchId;
            var timeBefore = match.ScheduledAt;
            var home = Guid.NewGuid();
            var away = Guid.NewGuid();

            // Act
            var result = match.ChangeTeams(home, away);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.HomeTournamentTeamId.Should().Be(home);
            match.AwayTournamentTeamId.Should().Be(away);
            match.PitchId.Should().Be(pitchBefore);
            match.ScheduledAt.Should().Be(timeBefore);
        }

        [Fact]
        public void ChangeTeams_ShouldFail_OnceTheMatchHasStarted()
        {
            // Arrange
            var match = CreateValidMatch();
            var homeBefore = match.HomeTournamentTeamId;
            match.Start(ValidUserId);

            // Act
            var result = match.ChangeTeams(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
            match.HomeTournamentTeamId.Should().Be(homeBefore);
        }

        [Fact]
        public void ChangeTeams_ShouldFail_WhenBothTeamsAreTheSame()
        {
            var match = CreateValidMatch();
            var teamId = Guid.NewGuid();

            var result = match.ChangeTeams(teamId, teamId);

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

        public static TheoryData<string> StartedStates => new() { "Live", "Paused", "Cancelled" };

        private static Match CreateMatchIn(string state)
        {
            var match = CreateValidMatch();
            if (state is "Live" or "Paused")
                match.Start(ValidUserId);
            if (state == "Paused")
                match.Pause(ValidUserId);
            if (state == "Cancelled")
                match.Cancel("deszcz", ValidUserId);
            match.ClearDomainEvents();
            return match;
        }

        [Theory]
        [MemberData(nameof(StartedStates))]
        public void AssignReferee_ShouldFail_OnceMatchHasStartedOrWasCancelled(string state)
        {
            var match = CreateMatchIn(state);
            var refereeBefore = match.RefereeId;

            var result = match.AssignReferee(Guid.NewGuid(), ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
            match.RefereeId.Should().Be(refereeBefore);
        }

        [Theory]
        [MemberData(nameof(StartedStates))]
        public void ScheduleChanges_ShouldFail_OnceMatchHasStartedOrWasCancelled(string state)
        {
            var match = CreateMatchIn(state);
            var pitchBefore = match.PitchId;
            var timeBefore = match.ScheduledAt;

            match.UpdateSchedule(Guid.NewGuid(), timeBefore.AddHours(1), ValidUserId).IsFailure.Should().BeTrue();
            match.AssignPitch(Guid.NewGuid(), ValidUserId).IsFailure.Should().BeTrue();
            match.Reschedule(timeBefore.AddHours(1), ValidUserId).IsFailure.Should().BeTrue();

            match.PitchId.Should().Be(pitchBefore);
            match.ScheduledAt.Should().Be(timeBefore);
            match.DomainEvents.Should().BeEmpty();
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

        // --- Finish: Walkower ---
        // Zamyka lukę z audytu: HomeScore/AwayScore zostawały 0:0 dla Walkower, więc tabela liczyła
        // go jako remis mimo ustawionego zwycięzcy.

        [Fact]
        public void Finish_ShouldSetHomeWinnerScore_WhenHomeTeamWinsByWalkover()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            // Act
            var result = match.Finish(MatchResultType.Walkover, match.HomeTournamentTeamId, 60, ValidUserId, walkoverWinnerScore: 3, walkoverLoserScore: 0);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(3);
            match.AwayScore.Should().Be(0);
        }

        [Fact]
        public void Finish_ShouldSetAwayWinnerScore_WhenAwayTeamWinsByWalkover()
        {
            // Zwycięzca walkoweru dostaje wynik zwycięzcy niezależnie od tego, czy jest drużyną
            // gospodarzy czy gości — WalkoverWinnerScore/LoserScore to nie dosłowne Home/Away.
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            // Act
            var result = match.Finish(MatchResultType.Walkover, match.AwayTournamentTeamId, 60, ValidUserId, walkoverWinnerScore: 3, walkoverLoserScore: 0);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(0);
            match.AwayScore.Should().Be(3);
        }

        [Fact]
        public void Finish_ShouldFail_WhenWalkoverHasNoWinner()
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            var result = match.Finish(MatchResultType.Walkover, null, 60, ValidUserId, walkoverWinnerScore: 3, walkoverLoserScore: 0);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.WinnerTournamentTeamId");
        }

        [Fact]
        public void Finish_ShouldFail_WhenWalkoverScoresNotProvided()
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            var result = match.Finish(MatchResultType.Walkover, match.HomeTournamentTeamId, 60, ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ResultType");
        }

        [Fact]
        public void Finish_ShouldNotAffectScore_WhenResultTypeIsNotWalkover()
        {
            // Regresja: upewniamy się, że dodanie logiki Walkower nie zaczęło nadpisywać wyniku
            // dla zwykłych meczów.
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.UpdateScore(2, 1);

            var result = match.Finish(MatchResultType.Regular, match.HomeTournamentTeamId, 60, ValidUserId, walkoverWinnerScore: 3, walkoverLoserScore: 0);

            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(2);
            match.AwayScore.Should().Be(1);
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
        public void SetManualScore_ShouldSetScoreAndResultType_WhenLive()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.ClearDomainEvents();

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
        public void SetManualScore_ShouldFail_WhenNegative()
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);

            var result = match.SetManualScore(-1, 0, ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Score");
        }

        [Fact]
        public void SetManualScore_ShouldFail_WhenMatchIsScheduled()
        {
            // Zamyka lukę z audytu: blokada obejmowała tylko Finished, więc dało się ustawić wynik
            // meczowi, który jeszcze się nie zaczął.
            var match = CreateValidMatch();

            var result = match.SetManualScore(2, 0, ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void UpdateScore_ShouldResetResultTypeFromManualDecisionToRegular_WhenRecalculatedFromEvents()
        {
            // Zamyka lukę z audytu: kolejny AddGoal/RevertMatchEvent po SetManualScore przeliczał wynik
            // z eventów, ale zostawiał ResultType=ManualDecision — mimo że wynik nie jest już ręczną decyzją.
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.SetManualScore(4, 1, ValidUserId);
            match.ClearDomainEvents();

            var result = match.UpdateScore(2, 0);

            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(2);
            match.AwayScore.Should().Be(0);
            match.ResultType.Should().Be(MatchResultType.Regular);
            match.DomainEvents.Should().ContainSingle(e => e is MatchScoreChanged);
        }

        [Fact]
        public void UpdateScore_ShouldResetResultTypeFromManualDecisionToRegular_EvenWhenScoreNumbersAreUnchanged()
        {
            // Ten sam wynik liczbowy, ale musi przestać być oznaczony jako ManualDecision, skoro
            // pochodzi teraz z przeliczenia eventów, a nie z ręcznego wpisu.
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.SetManualScore(2, 0, ValidUserId);
            match.ClearDomainEvents();

            var result = match.UpdateScore(2, 0);

            result.IsSuccess.Should().BeTrue();
            match.ResultType.Should().Be(MatchResultType.Regular);
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
            var result = match.CorrectFinishedScore(3, 2, null, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(3);
            match.AwayScore.Should().Be(2);
            match.DomainEvents.Should().ContainSingle(e => e is MatchScoreCorrected);
        }

        [Fact]
        public void CorrectFinishedScore_ShouldFail_WhenNegative()
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, null, 60, ValidUserId);

            var result = match.CorrectFinishedScore(-1, 0, null, ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Score");
        }

        [Fact]
        public void CorrectFinishedScore_ShouldUpdateWinnerTournamentTeamId_WhenCorrectionFlipsTheResult()
        {
            // Zamyka lukę z audytu: korekta zmieniała HomeScore/AwayScore, ale nigdy
            // WinnerTournamentTeamId, więc drabinka nie miała czym się kierować przy ponownym awansie.
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, match.HomeTournamentTeamId, 60, ValidUserId);
            match.ClearDomainEvents();

            // Act — wynik się odwraca, zwycięzcą jest teraz drużyna gości
            var result = match.CorrectFinishedScore(1, 3, match.AwayTournamentTeamId, ValidUserId);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
            var raised = match.DomainEvents.OfType<MatchScoreCorrected>().Single();
            raised.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
        }

        [Fact]
        public void CorrectFinishedScore_ShouldFail_WhenWinnerIsNeitherTeam()
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            match.Finish(MatchResultType.Regular, match.HomeTournamentTeamId, 60, ValidUserId);

            var result = match.CorrectFinishedScore(1, 1, Guid.NewGuid(), ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.WinnerTournamentTeamId");
        }

        [Fact]
        public void CorrectFinishedScore_ShouldFail_WhenNotFinished()
        {
            var match = CreateValidMatch();
            var result = match.CorrectFinishedScore(1, 1, null, ValidUserId);
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
            var result = match.CorrectFinishedScore(1, 1, null, ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.EditLockedAt");
        }

        // --- Cancel ---

        [Fact]
        public void Cancel_ShouldSetStatusToCancelled_WhenScheduled()
        {
            // Zamyka lukę z audytu: wycofanie drużyny/anulowanie turnieju musi realnie blokować
            // start/zakończenie meczu, a nie tylko "zostawić go tak jak jest".
            var match = CreateValidMatch();

            var result = match.Cancel("Turniej został anulowany.", ValidUserId);

            result.IsSuccess.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Cancelled);
            match.DomainEvents.Should().ContainSingle(e => e is MatchCancelled);
        }

        [Theory]
        [InlineData(nameof(Match.Start))]
        [InlineData(nameof(Match.Finish))]
        public void Cancel_ShouldFail_WhenMatchIsLiveOrFinished(string setupOperation)
        {
            var match = CreateValidMatch();
            match.Start(ValidUserId);
            if (setupOperation == nameof(Match.Finish))
                match.Finish(MatchResultType.Regular, null, 60, ValidUserId);

            var result = match.Cancel("test", ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void Cancel_ShouldFail_WhenAlreadyCancelled()
        {
            var match = CreateValidMatch();
            match.Cancel("test", ValidUserId);

            var result = match.Cancel("test", ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void Start_ShouldFail_AfterCancel()
        {
            // Ten test jest sercem fixa: Match.Status zostaje jedynym źródłem prawdy — po anulowaniu
            // (np. przez zdarzenie z Tournaments/Teams) Start() sam odmawia, bez potrzeby osobnego
            // cross-modułowego sprawdzenia statusu turnieju w każdej komendzie mutującej mecz.
            var match = CreateValidMatch();
            match.Cancel("Turniej został anulowany.", ValidUserId);

            var result = match.Start(ValidUserId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        // --- Version (optimistic concurrency) ---
        // Version musi rosnąć przy KAŻDEJ udanej mutacji, żeby EF mogło wykryć równoczesną edycję
        // (sędzia na słabym łączu + organizator korygujący harmonogram w tym samym momencie).

        [Fact]
        public void Version_ShouldBeZero_WhenJustCreated()
        {
            var match = CreateValidMatch();
            match.Version.Should().Be(0u);
        }

        [Theory]
        [InlineData(nameof(Match.Start))]
        [InlineData(nameof(Match.UpdateSchedule))]
        [InlineData(nameof(Match.AssignPitch))]
        [InlineData(nameof(Match.Reschedule))]
        [InlineData(nameof(Match.AssignReferee))]
        public void Version_ShouldIncrement_OnEachSuccessfulMutation(string operation)
        {
            // Arrange
            var match = CreateValidMatch();
            var before = match.Version;

            // Act
            var result = operation switch
            {
                nameof(Match.Start) => match.Start(ValidUserId),
                nameof(Match.UpdateSchedule) => match.UpdateSchedule(Guid.NewGuid(), ValidScheduledAt.AddHours(1), ValidUserId),
                nameof(Match.AssignPitch) => match.AssignPitch(Guid.NewGuid(), ValidUserId),
                nameof(Match.Reschedule) => match.Reschedule(ValidScheduledAt.AddDays(1), ValidUserId),
                nameof(Match.AssignReferee) => match.AssignReferee(Guid.NewGuid(), ValidUserId),
                _ => throw new InvalidOperationException()
            };

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.Version.Should().Be(before + 1);
        }

        [Fact]
        public void Version_ShouldIncrementOnEachStep_ThroughFullMatchLifecycle()
        {
            // Arrange
            var match = CreateValidMatch();
            match.Version.Should().Be(0u);

            // Act & Assert — Start
            match.Start(ValidUserId);
            match.Version.Should().Be(1u);

            // Pause
            match.Pause(ValidUserId);
            match.Version.Should().Be(2u);

            // Resume
            match.Resume(ValidUserId);
            match.Version.Should().Be(3u);

            // Gol (UpdateScore)
            match.UpdateScore(1, 0);
            match.Version.Should().Be(4u);

            // Finish
            match.Finish(MatchResultType.Regular, match.HomeTournamentTeamId, 60, ValidUserId);
            match.Version.Should().Be(5u);

            // Korekta wyniku po meczu
            match.CorrectFinishedScore(2, 0, null, ValidUserId);
            match.Version.Should().Be(6u);
        }

        [Fact]
        public void Version_ShouldNotIncrement_WhenOperationFails()
        {
            // Arrange — mecz jeszcze nie wystartowany, Pause() musi się nie udać przed jakąkolwiek mutacją
            var match = CreateValidMatch();

            // Act
            var result = match.Pause(ValidUserId);

            // Assert
            result.IsFailure.Should().BeTrue();
            match.Version.Should().Be(0u);
        }

        [Fact]
        public void Version_ShouldNotIncrement_WhenUpdateScoreIsANoOp()
        {
            // Arrange
            var match = CreateValidMatch();
            match.UpdateScore(1, 0);
            var versionAfterFirstUpdate = match.Version;

            // Act — ten sam wynik co już jest zapisany
            var result = match.UpdateScore(1, 0);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.Version.Should().Be(versionAfterFirstUpdate);
        }
        private static Match LiveKnockout(int home, int away)
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, StageType.Knockout).Value;
            match.Start(Guid.NewGuid());
            match.UpdateScore(home, away);
            return match;
        }

        [Fact]
        public void Finish_Knockout_ShouldRejectDrawInRegularTime()
        {
            var match = LiveKnockout(1, 1);

            var result = match.Finish(MatchResultType.Regular, null, 10, ValidUserId);

            result.IsFailure.Should().BeTrue();
            match.Status.Should().Be(MatchStatus.Live);
        }

        [Fact]
        public void Finish_Knockout_ShouldDeriveWinnerFromScore_AndRejectWrongWinner()
        {
            var match = LiveKnockout(2, 1);
            match.Finish(MatchResultType.ExtraTime, match.AwayTournamentTeamId, 10, ValidUserId).IsFailure.Should().BeTrue();

            match.Finish(MatchResultType.ExtraTime, null, 10, ValidUserId).IsSuccess.Should().BeTrue();
            match.WinnerTournamentTeamId.Should().Be(match.HomeTournamentTeamId);
        }

        [Fact]
        public void Finish_Penalties_ShouldStoreShootoutAndPickWinner()
        {
            var match = LiveKnockout(1, 1);

            var result = match.Finish(MatchResultType.Penalties, null, 10, ValidUserId, homePenaltyScore: 3, awayPenaltyScore: 4);

            result.IsSuccess.Should().BeTrue();
            match.HomePenaltyScore.Should().Be(3);
            match.AwayPenaltyScore.Should().Be(4);
            match.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
        }

        [Theory]
        [InlineData(2, 1, 4, 3)]   // karne tylko przy remisie
        [InlineData(1, 1, 3, 3)]   // karne bez rozstrzygnięcia
        [InlineData(1, 1, null, 3)] // brak wyniku karnych
        public void Finish_Penalties_ShouldRejectInvalidShootout(int home, int away, int? homePens, int? awayPens)
        {
            var match = LiveKnockout(home, away);

            match.Finish(MatchResultType.Penalties, null, 10, ValidUserId, homePenaltyScore: homePens, awayPenaltyScore: awayPens)
                .IsFailure.Should().BeTrue();
        }
    }
}
