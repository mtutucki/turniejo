using FluentAssertions;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.UnitTests.Entities
{
    public sealed class TournamentCategoryTests
    {
        private static readonly Guid validId = Guid.NewGuid();
        private static readonly Guid validOrganizationId = Guid.NewGuid();
        private static readonly Guid validTournamentId = Guid.NewGuid();
        private static readonly Guid validCreatedByUserId = Guid.NewGuid();

        private const string validName = "Test Category";
        private static readonly DateTime validMinBirthDate = DateTime.Now;
        private static readonly DateTime validMaxBirthDate = DateTime.Now.AddDays(1);
        private const int validOrder = 1;

        private static Result<TournamentCategory> CreateValidTournamentCategory()
        {
            return TournamentCategory.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, validName, validMinBirthDate, validMaxBirthDate, validOrder);
        }

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            //Act
            var result = CreateValidTournamentCategory();

            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(validId);
            result.Value.Name.Should().Be(validName);
            result.Value.MinBirthDate.Should().Be(validMinBirthDate);
            result.Value.MaxBirthDate.Should().Be(validMaxBirthDate);
            result.Value.Order.Should().Be(validOrder);
        }

        [Fact]
        public void Create_ShouldSucceed_WhenBirthDatesAreNull()
        {
            //Act
            var result = TournamentCategory.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, validName, null, null, validOrder);
            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(validId);
            result.Value.Name.Should().Be(validName);
            result.Value.MinBirthDate.Should().BeNull();
            result.Value.MaxBirthDate.Should().BeNull();
            result.Value.Order.Should().Be(validOrder);
        }

        [Fact]
        public void Create_ShouldAddTournamentCategoryAddedEvent()
        {
            //Act
            var result = CreateValidTournamentCategory();
            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.DomainEvents.Should().ContainSingle(e => e is TournamentCategoryAdded);
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            //Act
            var result = TournamentCategory.Create(Guid.Empty, validOrganizationId, validTournamentId, validCreatedByUserId, validName, validMinBirthDate, validMaxBirthDate, validOrder);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
            result.Error.Message.Should().Be("TournamentCategory id cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenOrganizationIdIsEmpty()
        {
            //Act
            var result = TournamentCategory.Create(validId, Guid.Empty, validTournamentId, validCreatedByUserId, validName, validMinBirthDate, validMaxBirthDate, validOrder);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.OrganizationId");
            result.Error.Message.Should().Be("TournamentCategory organizationId cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenCreatedByUserIdIsEmpty()
        {
            //Act
            var result = TournamentCategory.Create(validId, validOrganizationId, validTournamentId, Guid.Empty, validName, validMinBirthDate, validMaxBirthDate, validOrder);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.CreatedByUserId");
            result.Error.Message.Should().Be("TournamentCategory createdByUserId cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenTournamentIdIsEmpty()
        {
            //Act
            var result = TournamentCategory.Create(validId, validOrganizationId, Guid.Empty, validCreatedByUserId, validName, validMinBirthDate, validMaxBirthDate, validOrder);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentId");
            result.Error.Message.Should().Be("TournamentCategory tournamentId cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenNameIsEmpty()
        {
            //Act
            var result = TournamentCategory.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, string.Empty, validMinBirthDate, validMaxBirthDate, validOrder);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            result.Error.Message.Should().Be("TournamentCategory name cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenNameIsWhitespace()
        {
            //Act
            var result = TournamentCategory.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, "   ", validMinBirthDate, validMaxBirthDate, validOrder);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            result.Error.Message.Should().Be("TournamentCategory name cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenMinBirthDateIsAfterMaxBirthDate()
        {
            //Act
            var result = TournamentCategory.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, validName, validMaxBirthDate, validMinBirthDate, validOrder);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.BirthDate");
            result.Error.Message.Should().Be("TournamentCategory minBirthDate cannot be greater than maxBirthDate");
        }

        [Fact]
        public void Create_ShouldSucceed_WhenMinBirthDateEqualsMaxBirthDate()
        {
            //Act
            var result = TournamentCategory.Create(validId, validOrganizationId, validTournamentId, validCreatedByUserId, validName, validMinBirthDate, validMinBirthDate, validOrder);
            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MinBirthDate.Should().Be(validMinBirthDate);
            result.Value.MaxBirthDate.Should().Be(validMinBirthDate);
        }
    }
}
