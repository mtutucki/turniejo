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
    }
}