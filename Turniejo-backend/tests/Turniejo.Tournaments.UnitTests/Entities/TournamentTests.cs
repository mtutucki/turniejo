using FluentAssertions;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.UnitTests.Entities
{
    public sealed class TournamentTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidOrganizationId => Guid.NewGuid();
        private static Guid ValidUserId => Guid.NewGuid();
        private const string ValidName = "Turniej";
        private static DateTimeOffset ValidStartAt => DateTimeOffset.UtcNow.AddDays(1);
        private static DateTimeOffset ValidEndAt => DateTimeOffset.UtcNow.AddDays(2);
        private const int ValidTeamLimit = 16;
        private const string ValidDescription = "Opis turnieju";

        private static Tournament CreateTournamentWithStatus(TournamentStatus targetStatus)
        {
            var tournament = Tournament.Create(
                ValidId, ValidOrganizationId, ValidUserId,
                ValidName, ValidStartAt, ValidEndAt,
                ValidTeamLimit, ValidDescription).Value;

            var path = new[]
            {
                TournamentStatus.RegistrationOpen,
                TournamentStatus.RegistrationClosed,
                TournamentStatus.ScheduleGenerated,
                TournamentStatus.InProgress,
                TournamentStatus.Finished
            };

            foreach (var status in path)
            {
                if (tournament.Status == targetStatus) break;

                if (status == TournamentStatus.ScheduleGenerated)
                    tournament.SetFormat(TournamentFormat.Group);

                tournament.ChangeStatus(status, ValidUserId);
            }

            return tournament;
        }

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            //Arrange
            var validId = ValidId;
            var organizationId = ValidOrganizationId;
            var userId = ValidUserId;

            //Act
            var result = Tournament.Create(validId, organizationId, userId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription);

            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(validId);
            result.Value.Name.Should().Be(ValidName);
            result.Value.Status.Should().Be(TournamentStatus.Draft);
        }

        [Fact]
        public void GeneratedCodes_ShouldBeUnique()
        {
            // Arrange
            var tournament1 = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var tournament2 = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName + " 2", ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            // Act
            var codes1 = new HashSet<string> { tournament1.OrganizerCode, tournament1.RefereeCode, tournament1.CoachCode, tournament1.ViewerCode };
            var codes2 = new HashSet<string> { tournament2.OrganizerCode, tournament2.RefereeCode, tournament2.CoachCode, tournament2.ViewerCode };
            // Assert
            codes1.Count.Should().Be(4);
            codes2.Count.Should().Be(4);
            codes1.Intersect(codes2).Should().BeEmpty();
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            //Act
            var result = Tournament.Create(Guid.Empty, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
            result.Error.Message.Should().Be("Tournament Id cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenOrganizationIdIsEmpty()
        {
            //Act
            var result = Tournament.Create(ValidId, Guid.Empty, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.OrganizationId");
            result.Error.Message.Should().Be("Organization Id cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenNameIsEmpty()
        {
            //Act
            var result = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, string.Empty, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            result.Error.Message.Should().Be("Tournament name cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenEndAtIsBeforeStartAt()
        {
            //Act
            var result = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidEndAt, ValidStartAt, ValidTeamLimit, ValidDescription);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.EndAt");
            result.Error.Message.Should().Be("Tournament end date cannot be before start date");
        }

        [Fact]
        public void Create_ShouldFail_WhenTeamLimitIsNegative()
        {
            //Act
            var result = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, -1, ValidDescription);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TeamLimit");
            result.Error.Message.Should().Be("Tournament team limit cannot be negative");
        }

        [Fact]
        public void Create_ShouldAddTournamentCreatedEvent()
        {
            //Act
            var result = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription);
            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is TournamentCreated);
        }

        [Fact]
        public void Create_ShouldGenerateUniqueSlug()
        {
            //Act
            var result1 = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription);
            var result2 = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName + " 2", ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription);
            //Assert
            result1.IsSuccess.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();
            result1.Value.Slug.Should().NotBe(result2.Value.Slug);
        }

        [Fact]
        public void ChangeStatus_ShouldUpdateStatus_WhenValid()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var newStatus = TournamentStatus.RegistrationOpen;
            // Act
            var result = tournament.ChangeStatus(newStatus, ValidUserId);
            // Assert
            result.IsSuccess.Should().BeTrue();
            tournament.Status.Should().Be(newStatus);
        }

        [Fact]
        public void ChangeStatus_ShouldFail_WhenStatusIsSame()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var newStatus = tournament.Status; // same status
            // Act
            var result = tournament.ChangeStatus(newStatus, ValidUserId);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Status");
            result.Error.Message.Should().Be("Tournament status is already set to the specified value");
        }

        [Fact]
        public void ChangeStatus_ShouldFail_WhenStatusIsCancelled()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            tournament.ChangeStatus(TournamentStatus.Cancelled, ValidUserId);
            var newStatus = TournamentStatus.RegistrationOpen; // trying to change from Cancelled
            // Act
            var result = tournament.ChangeStatus(newStatus, ValidUserId);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Status");
            result.Error.Message.Should().Be("Tournament status cannot be changed after cancellation");
        }
        [Fact]
        public void ChangeStatus_ShouldFail_WhenStatusIsFinished()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            tournament.ChangeStatus(TournamentStatus.RegistrationOpen, ValidUserId);
            tournament.ChangeStatus(TournamentStatus.RegistrationClosed, ValidUserId);
            tournament.SetFormat(TournamentFormat.Group);
            tournament.ChangeStatus(TournamentStatus.ScheduleGenerated, ValidUserId);
            tournament.ChangeStatus(TournamentStatus.InProgress, ValidUserId);
            tournament.ChangeStatus(TournamentStatus.Finished, ValidUserId);
            var newStatus = TournamentStatus.RegistrationOpen;
            // Act
            var result = tournament.ChangeStatus(newStatus, ValidUserId);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Status");
            result.Error.Message.Should().Be("Tournament status cannot be changed after it has finished");
        }

        [Theory]
        [InlineData(TournamentStatus.Draft, TournamentStatus.InProgress)]
        [InlineData(TournamentStatus.Draft, TournamentStatus.Finished)]
        [InlineData(TournamentStatus.Draft, TournamentStatus.RegistrationClosed)]
        [InlineData(TournamentStatus.RegistrationOpen, TournamentStatus.InProgress)]
        [InlineData(TournamentStatus.RegistrationOpen, TournamentStatus.Draft)]
        [InlineData(TournamentStatus.RegistrationClosed, TournamentStatus.InProgress)]
        [InlineData(TournamentStatus.RegistrationClosed, TournamentStatus.RegistrationOpen)]
        [InlineData(TournamentStatus.ScheduleGenerated, TournamentStatus.Finished)]
        [InlineData(TournamentStatus.ScheduleGenerated, TournamentStatus.Draft)]
        public void ChangeStatus_ShouldFail_WhenTransitionIsNotAllowed(TournamentStatus currentStatus, TournamentStatus newStatus)
        {
            // Arrange
            var tournament = CreateTournamentWithStatus(currentStatus);
            // doprowadź do currentStatus
            // Act
            var result = tournament.ChangeStatus(newStatus, ValidUserId);
            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public void ChangeStatus_ShouldAddTournamentStatusChangedEvent_WhenValid()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var newStatus = TournamentStatus.RegistrationOpen;
            // Act
            var result = tournament.ChangeStatus(newStatus, ValidUserId);
            // Assert
            result.IsSuccess.Should().BeTrue();
            tournament.DomainEvents.Should().ContainSingle(e => e is TournamentStatusChanged);
        }

        [Fact]
        public void UpdateDetails_ShouldUpdateTournament_WhenValid()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var newName = "Updated Tournament";
            var newStartAt = ValidStartAt.AddDays(1);
            var newEndAt = ValidEndAt.AddDays(1);
            var newTeamLimit = 32;
            var newDescription = "Updated description";
            // Act
            var result = tournament.UpdateDetails(newName, newStartAt, newEndAt, newTeamLimit, newDescription);
            // Assert
            result.IsSuccess.Should().BeTrue();
            tournament.Name.Should().Be(newName);
            tournament.StartAt.Should().Be(newStartAt);
            tournament.EndAt.Should().Be(newEndAt);
            tournament.TeamLimit.Should().Be(newTeamLimit);
            tournament.Description.Should().Be(newDescription);
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenTournamentIsCancelled()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            tournament.ChangeStatus(TournamentStatus.Cancelled, ValidUserId);
            // Act
            var result = tournament.UpdateDetails("New Name", ValidStartAt, ValidEndAt, ValidTeamLimit, "New Description");
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Status");
            result.Error.Message.Should().Be("Tournament status cannot be changed after cancellation");
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenTournamentIsFinished()
        {
            // Arrange
            var tournament = CreateTournamentWithStatus(TournamentStatus.Finished);
            // Act
            var result = tournament.UpdateDetails("New Name", ValidStartAt, ValidEndAt, ValidTeamLimit, "New Description");
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Status");
            result.Error.Message.Should().Be("Tournament status cannot be changed after it has finished");
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenNameIsEmpty()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            // Act
            var result = tournament.UpdateDetails(string.Empty, ValidStartAt, ValidEndAt, ValidTeamLimit, "New Description");
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            result.Error.Message.Should().Be("Tournament name cannot be empty");
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenEndAtIsBeforeStartAt()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            // Act
            var result = tournament.UpdateDetails("New Name", ValidEndAt, ValidStartAt, ValidTeamLimit, "New Description");
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.EndAt");
            result.Error.Message.Should().Be("Tournament end date cannot be before start date");
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenTeamLimitIsNegative()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            // Act
            var result = tournament.UpdateDetails("New Name", ValidStartAt, ValidEndAt, -1, "New Description");
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TeamLimit");
            result.Error.Message.Should().Be("Tournament team limit cannot be negative");
        }

        [Fact]
        public void SetFormat_ShouldUpdateFormat_WhenValid()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var newFormat = TournamentFormat.Knockout;
            // Act
            tournament.SetFormat(newFormat);
            // Assert
            tournament.Format.Should().Be(newFormat);
        }

        [Fact]
        public void SetFormat_ShouldFail_WhenTournamentIsScheduleGeneratedOrLater()
        {
            // Arrange
            var tournament = CreateTournamentWithStatus(TournamentStatus.ScheduleGenerated);

            var newFormat = TournamentFormat.Knockout;
            // Act
            var result = tournament.SetFormat(newFormat);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Format");
            result.Error.Message.Should().Be("Cannot change format after schedule has been generated.");
        }

        [Fact]
        public void UpdateSettings_ShouldUpdateSettings_WhenValid()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var settings = Domain.Entities.TournamentSettings.CreateDefault(tournament.Id);

            // Act
            var result = tournament.UpdateSettings(settings.PointsForWin, settings.PointsForDraw, settings.PointsForLoss, settings.MatchDurationMinutes, settings.BreakDurationMinutes, settings.IsThirdPlaceMatchEnabled, settings.RefereeEditWindowMinutes, settings.WalkoverHomeScore, settings.WalkoverAwayScore, settings.CanCoachEditSquadAfterStart, Guid.NewGuid());

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Theory]
        [InlineData(TournamentStatus.InProgress)]
        [InlineData(TournamentStatus.Finished)]
        [InlineData(TournamentStatus.Cancelled)]
        public void UpdateSettings_ShouldFail_WhenStatusIsInProgressOrFurther(TournamentStatus tournamentStatus)
        {
            // Arrange
            var tournament = CreateTournamentWithStatus(tournamentStatus);
            var settings = Domain.Entities.TournamentSettings.CreateDefault(tournament.Id);

            // Act
            var result = tournament.UpdateSettings(settings.PointsForWin, settings.PointsForDraw, settings.PointsForLoss, settings.MatchDurationMinutes, settings.BreakDurationMinutes, settings.IsThirdPlaceMatchEnabled, settings.RefereeEditWindowMinutes, settings.WalkoverHomeScore, settings.WalkoverAwayScore, settings.CanCoachEditSquadAfterStart, Guid.NewGuid());

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public void UpdateSettings_ShouldAddTournamentSettingsUpdatedEvent_WhenValid()
        {
            // Arrange
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;
            var settings = Domain.Entities.TournamentSettings.CreateDefault(tournament.Id);

            // Act
            var result = tournament.UpdateSettings(settings.PointsForWin, settings.PointsForDraw, settings.PointsForLoss, settings.MatchDurationMinutes, settings.BreakDurationMinutes, settings.IsThirdPlaceMatchEnabled, settings.RefereeEditWindowMinutes, settings.WalkoverHomeScore, settings.WalkoverAwayScore, settings.CanCoachEditSquadAfterStart, Guid.NewGuid());

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournament.DomainEvents.Should().ContainSingle(t => t is TournamentSettingsUpdated);
        }

        [Fact]
        public void UpdatePublicSettings_ShouldUpdateAllFlags_WhenValid()
        {
            // Zamyka lukę z audytu testów: UpdatePublicSettings (delegujący do
            // TournamentPublicSettings.Update) nie miał żadnego testu na poziomie aggregate roota,
            // tylko pośrednio przez test handlera komendy.
            var tournament = Tournament.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, ValidStartAt, ValidEndAt, ValidTeamLimit, ValidDescription).Value;

            var result = tournament.UpdatePublicSettings(true, false, false, true, false, true, false, true);

            result.IsSuccess.Should().BeTrue();
            tournament.PublicSettings.IsPublic.Should().BeTrue();
            tournament.PublicSettings.ShowTeams.Should().BeFalse();
            tournament.PublicSettings.ShowPlayers.Should().BeFalse();
            tournament.PublicSettings.ShowSchedule.Should().BeTrue();
            tournament.PublicSettings.ShowStandings.Should().BeFalse();
            tournament.PublicSettings.ShowBracket.Should().BeTrue();
            tournament.PublicSettings.ShowLiveMatches.Should().BeFalse();
            tournament.PublicSettings.ShowStats.Should().BeTrue();
        }

        [Fact]
        public void RevertScheduleGeneration_ShouldReturnToRegistrationClosed_AndRaiseStatusChanged()
        {
            var tournament = CreateTournamentWithStatus(TournamentStatus.ScheduleGenerated);
            tournament.ClearDomainEvents();

            var result = tournament.RevertScheduleGeneration(ValidUserId);

            result.IsSuccess.Should().BeTrue();
            tournament.Status.Should().Be(TournamentStatus.RegistrationClosed);
            tournament.DomainEvents.OfType<TournamentStatusChanged>().Should().ContainSingle(e =>
                e.OldStatus == TournamentStatus.ScheduleGenerated && e.NewStatus == TournamentStatus.RegistrationClosed);
        }

        [Theory]
        [InlineData(TournamentStatus.RegistrationClosed)]
        [InlineData(TournamentStatus.InProgress)]
        [InlineData(TournamentStatus.Finished)]
        public void RevertScheduleGeneration_ShouldFail_OutsideScheduleGenerated(TournamentStatus status)
        {
            var tournament = CreateTournamentWithStatus(status);

            var result = tournament.RevertScheduleGeneration(ValidUserId);

            result.IsFailure.Should().BeTrue();
            tournament.Status.Should().Be(status);
        }
    }
}