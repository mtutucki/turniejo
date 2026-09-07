using FluentAssertions;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Teams.UnitTests.Entities
{
    public sealed class TeamTests
    {
        private static Guid ValidId => Guid.NewGuid();
        private static Guid ValidOrganizationId => Guid.NewGuid();
        private static Guid ValidUserId => Guid.NewGuid();
        private const string ValidName = "FC Testowy";

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Arrange
            var validId = ValidId;
            var organizationId = ValidOrganizationId;

            // Act
            var result = Team.Create(validId, organizationId, ValidUserId, ValidName, "FCT", "https://example.com/logo.png");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(validId);
            result.Value.OrganizationId.Should().Be(organizationId);
            result.Value.Name.Should().Be(ValidName);
            result.Value.ShortName.Should().Be("FCT");
            result.Value.LogoUrl.Should().Be("https://example.com/logo.png");
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            // Act
            var result = Team.Create(Guid.Empty, ValidOrganizationId, ValidUserId, ValidName, null, null);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
        }

        [Fact]
        public void Create_ShouldFail_WhenOrganizationIdIsEmpty()
        {
            // Act
            var result = Team.Create(ValidId, Guid.Empty, ValidUserId, ValidName, null, null);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.OrganizationId");
        }

        [Fact]
        public void Create_ShouldFail_WhenCreatedByUserIdIsEmpty()
        {
            // Act
            var result = Team.Create(ValidId, ValidOrganizationId, Guid.Empty, ValidName, null, null);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.CreatedByUserId");
        }

        [Fact]
        public void Create_ShouldFail_WhenNameIsEmpty()
        {
            // Act
            var result = Team.Create(ValidId, ValidOrganizationId, ValidUserId, string.Empty, null, null);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
        }

        [Fact]
        public void Create_ShouldAddTeamCreatedEvent()
        {
            // Act
            var result = Team.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, null, null);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is TeamCreated);
        }

        [Fact]
        public void UpdateDetails_ShouldUpdateTeam_WhenValid()
        {
            // Arrange
            var team = Team.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, "FCT", null).Value;

            // Act
            var result = team.UpdateDetails("Nowa nazwa", "NN");

            // Assert
            result.IsSuccess.Should().BeTrue();
            team.Name.Should().Be("Nowa nazwa");
            team.ShortName.Should().Be("NN");
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenNameIsEmpty()
        {
            // Arrange
            var team = Team.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, null, null).Value;

            // Act
            var result = team.UpdateDetails(string.Empty, null);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
        }

        [Fact]
        public void UpdateLogo_ShouldUpdateLogo_WhenValid()
        {
            // Arrange
            var team = Team.Create(ValidId, ValidOrganizationId, ValidUserId, ValidName, null, null).Value;

            // Act
            var result = team.UpdateLogo("https://example.com/new-logo.png");

            // Assert
            result.IsSuccess.Should().BeTrue();
            team.LogoUrl.Should().Be("https://example.com/new-logo.png");
        }
    }
}
