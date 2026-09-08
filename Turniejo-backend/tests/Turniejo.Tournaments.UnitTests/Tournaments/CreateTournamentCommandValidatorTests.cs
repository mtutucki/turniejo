using FluentAssertions;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;

namespace Turniejo.Tournaments.UnitTests.Tournaments
{
    public sealed class CreateTournamentCommandValidatorTests
    {
        private readonly CreateTournamentCommandValidator _validator = new();

        private static CreateTournamentCommand ValidCommand(string? description = null) =>
            new(Guid.NewGuid(), "Puchar Wiosny", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2), 16, description);

        [Fact]
        public void Validate_ShouldSucceed_WhenCommandIsValid()
        {
            var result = _validator.Validate(ValidCommand());

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldFail_WhenDescriptionExceedsMaxLength()
        {
            // Zamyka lukę z audytu: brak limitu długości opisu turnieju.
            var command = ValidCommand(new string('a', 501));

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTournamentCommand.Description));
        }
    }
}
