using FluentAssertions;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.UnitTests.Entities
{
    public sealed class TournamentGroupTests
    {
        private static readonly Guid validId = Guid.NewGuid();
        private static readonly Guid validTournamentId = Guid.NewGuid();
        private static readonly Guid validOrganizationId = Guid.NewGuid();
        private static readonly Guid validCreatedByUserId = Guid.NewGuid();
        private static readonly Guid validCategoryId = Guid.NewGuid();
        private const string validName = "Test Group";
        private const int validOrder = 1;

        private static Result<TournamentGroup> CreateValidTournamentGroup()
        {
            return TournamentGroup.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, validCategoryId, validName, validOrder);
        }

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            // Act
            var result = CreateValidTournamentGroup();
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(validId);
            result.Value.TournamentId.Should().Be(validTournamentId);
            result.Value.CategoryId.Should().Be(validCategoryId);
            result.Value.Name.Should().Be(validName);
            result.Value.Order.Should().Be(validOrder);
        }

        [Fact]
        public void Create_ShouldAddTournamentGroupAddedEvent()
        {
            // Act
            var result = CreateValidTournamentGroup();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is TournamentGroupAdded);
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            // Act
            var result = TournamentGroup.Create(Guid.Empty, validOrganizationId, validTournamentId, validCreatedByUserId, validCategoryId, validName, validOrder);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
            result.Error.Message.Should().Be("TournamentGroup ID cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenOrganizationIdIsEmpty()
        {
            // Act
            var result = TournamentGroup.Create(validId, Guid.Empty, validTournamentId, validCreatedByUserId, validCategoryId, validName, validOrder);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.OrganizationId");
            result.Error.Message.Should().Be("TournamentGroup organizationId cannot be empty");
        }
        [Fact]
        public void Create_ShouldFail_WhenCreatedByUserIdIsEmpty()
        {
            // Act
            var result = TournamentGroup.Create(validId, validOrganizationId, validTournamentId, Guid.Empty, validCategoryId, validName, validOrder);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.CreatedByUserId");
            result.Error.Message.Should().Be("TournamentGroup createdByUserId cannot be empty");
        }
        [Fact]
        public void Create_ShouldFail_WhenTournamentIdIsEmpty()
        {
            // Act
            var result = TournamentGroup.Create(validId, validOrganizationId, Guid.Empty, validCreatedByUserId, validCategoryId, validName, validOrder);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentId");
            result.Error.Message.Should().Be("TournamentGroup tournamentId cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenCategoryIdIsEmpty()
        {
            // Act
            var result = TournamentGroup.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, Guid.Empty, validName, validOrder);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.CategoryId");
            result.Error.Message.Should().Be("TournamentGroup categoryId cannot be empty");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  ")]
        public void Create_ShouldFail_WhenNameIsEmptyOrWhitespace(string name)
        {
            // Act
            var result = TournamentGroup.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, validCategoryId, name, validOrder);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            result.Error.Message.Should().Be("TournamentGroup name cannot be empty");
        }

        [Fact]
        public void UpdateDetails_ShouldChangeNameAndOrder_WhenDataIsValid()
        {
            // Arrange
            var group = CreateValidTournamentGroup().Value;

            // Act
            var result = group.UpdateDetails("Grupa B", 3);

            // Assert
            result.IsSuccess.Should().BeTrue();
            group.Name.Should().Be("Grupa B");
            group.Order.Should().Be(3);
        }

        [Fact]
        public void UpdateDetails_ShouldFail_WhenNameIsEmpty()
        {
            // Arrange
            var group = CreateValidTournamentGroup().Value;

            // Act
            var result = group.UpdateDetails(" ", 3);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            group.Name.Should().Be(validName);
        }
    }
}
