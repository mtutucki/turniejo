using FluentAssertions;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Entities
{
    public sealed class TournamentSettingsTests
    {
        private static readonly Guid validTournamentId = Guid.NewGuid();
        private static readonly int validPointsForWin = 3;
        private static readonly int validPointsForDraw = 1;
        private static readonly int validPointsForLoss = 0;
        private static readonly int validMatchDurationMinutes = 30;
        private static readonly int validBreakDurationMinutes = 5;

        private static readonly bool validIsThirdPlaceMatchEnabled = false;
        private static readonly int validRefereeEditWindowMinutes = 60;
        private static readonly int validWalkoverHomeScore = 3;
        private static readonly int validWalkoverAwayScore = 0;
        private static readonly bool validCanCoachEditSquadAfterStart = false;

        private static TournamentSettings CreateValidTournamentSettings()
        {
            return TournamentSettings.CreateDefault(validTournamentId);
        }

        [Fact]
        public void CreateDefault_ShouldSucceed_WhenDataIsValid()
        {
            // Act
            var result = CreateValidTournamentSettings();

            // Assert
            result.TournamentId.Should().Be(validTournamentId);
            result.PointsForWin.Should().Be(validPointsForWin);
            result.PointsForDraw.Should().Be(validPointsForDraw);
            result.PointsForLoss.Should().Be(validPointsForLoss);
            result.MatchDurationMinutes.Should().Be(validMatchDurationMinutes);
            result.BreakDurationMinutes.Should().Be(validBreakDurationMinutes);
            result.IsThirdPlaceMatchEnabled.Should().Be(validIsThirdPlaceMatchEnabled);
            result.RefereeEditWindowMinutes.Should().Be(validRefereeEditWindowMinutes);
            result.WalkoverHomeScore.Should().Be(validWalkoverHomeScore);
            result.WalkoverAwayScore.Should().Be(validWalkoverAwayScore);
            result.CanCoachEditSquadAfterStart.Should().Be(validCanCoachEditSquadAfterStart);
        }

        [Fact]
        public void Update_ShouldSucceed_WhenDataIsValid()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            var updateResult = tournamentSettings.Update(validPointsForWin, validPointsForDraw, validPointsForLoss, validMatchDurationMinutes,
                validBreakDurationMinutes, validIsThirdPlaceMatchEnabled, validRefereeEditWindowMinutes,
                validWalkoverHomeScore, validWalkoverAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Update_ShouldFailure_WhenPointsForWinAreLessThanPointsForDraw()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            int invalidPointsForWin = 0;
            int invalidPointsForDraw = 1;
            var updateResult = tournamentSettings.Update(invalidPointsForWin, invalidPointsForDraw, validPointsForLoss, validMatchDurationMinutes,
                validBreakDurationMinutes, validIsThirdPlaceMatchEnabled, validRefereeEditWindowMinutes,
                validWalkoverHomeScore, validWalkoverAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be("Validation.PointsForWin");
        }

        [Fact]
        public void Update_ShouldFailure_WhenPointsForDrawAreLessThanPointsForLoss()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            int invalidPointsForDraw = 0;
            int invalidPointsForLoss = 1;
            var updateResult = tournamentSettings.Update(validPointsForWin, invalidPointsForDraw, invalidPointsForLoss, validMatchDurationMinutes,
                validBreakDurationMinutes, validIsThirdPlaceMatchEnabled, validRefereeEditWindowMinutes,
                validWalkoverHomeScore, validWalkoverAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be("Validation.PointsForDraw");
        }

        [Fact]
        public void Update_ShouldFailure_WhenPointsForLossAreNegative()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            int invalidPointsForLoss = -1;
            var updateResult = tournamentSettings.Update(validPointsForWin, validPointsForDraw, invalidPointsForLoss, validMatchDurationMinutes,
                validBreakDurationMinutes, validIsThirdPlaceMatchEnabled, validRefereeEditWindowMinutes,
                validWalkoverHomeScore, validWalkoverAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be("Validation.PointsForLoss");
        }

        [Fact]
        public void Update_ShouldFailure_WhenMatchDurationMinutesAreInvalid()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            int invalidMatchDurationMinutes = 0;
            var updateResult = tournamentSettings.Update(validPointsForWin, validPointsForDraw, validPointsForLoss, invalidMatchDurationMinutes,
                validBreakDurationMinutes, validIsThirdPlaceMatchEnabled, validRefereeEditWindowMinutes,
                validWalkoverHomeScore, validWalkoverAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be("Validation.MatchDurationMinutes");
        }

        [Fact]
        public void Update_ShouldFailure_WhenWalkowerHomeScoreEqualsWalkowerAwayScore()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            int invalidWalkowerHomeScore = 0;
            int imvalidWalkowerAwayScore = 0;
            var updateResult = tournamentSettings.Update(validPointsForWin, validPointsForDraw, validPointsForLoss, validMatchDurationMinutes,
                validBreakDurationMinutes, validIsThirdPlaceMatchEnabled, validRefereeEditWindowMinutes,
                invalidWalkowerHomeScore, imvalidWalkowerAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be("Validation.WalkoverHomeScore");
        }

        [Fact]
        public void Update_ShouldFailure_WhenBreakDurationMinutesIsInvalid()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            int invalidBreakDurationMinutes = -1;
            var updateResult = tournamentSettings.Update(validPointsForWin, validPointsForDraw, validPointsForLoss, validMatchDurationMinutes,
                invalidBreakDurationMinutes, validIsThirdPlaceMatchEnabled, validRefereeEditWindowMinutes,
                validWalkoverHomeScore, validWalkoverAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be("Validation.BreakDurationMinutes");
        }

        [Fact]
        public void Update_ShouldFailure_WhenRefereeEditWindowMinutesIsInvalid()
        {
            // Act
            var tournamentSettings = CreateValidTournamentSettings();
            int invalidRefereeEditWindowMinutes = -1;
            var updateResult = tournamentSettings.Update(validPointsForWin, validPointsForDraw, validPointsForLoss, validMatchDurationMinutes,
                validBreakDurationMinutes, validIsThirdPlaceMatchEnabled, invalidRefereeEditWindowMinutes,
                validWalkoverHomeScore, validWalkoverAwayScore, validCanCoachEditSquadAfterStart);

            // Assert
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be("Validation.RefereeEditWindowMinutes");
        }
    }
}
