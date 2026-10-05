using FluentAssertions;
using Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory;

namespace Turniejo.Tournaments.UnitTests.TournamentsCategories
{
    public sealed class DeleteTournamentCategoryCommandValidatorTests
    {
        private readonly DeleteTournamentCategoryCommandValidator _validator = new();

        [Fact]
        public void Validate_ShouldSucceed_WhenIdsAreProvided()
        {
            var command = new DeleteTournamentCategoryCommand(Guid.NewGuid(), Guid.NewGuid());

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldFail_WhenTournamentIdIsEmpty()
        {
            var command = new DeleteTournamentCategoryCommand(Guid.Empty, Guid.NewGuid());

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(DeleteTournamentCategoryCommand.TournamentId));
        }

        [Fact]
        public void Validate_ShouldFail_WhenCategoryIdIsEmpty()
        {
            var command = new DeleteTournamentCategoryCommand(Guid.NewGuid(), Guid.Empty);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(DeleteTournamentCategoryCommand.CategoryId));
        }
    }
}
