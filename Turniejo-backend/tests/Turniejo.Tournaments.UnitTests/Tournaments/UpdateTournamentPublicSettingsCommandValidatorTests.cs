using FluentAssertions;
using Turniejo.Tournaments.Application.Tournaments.UpdateTournamentPublicSettings;

namespace Turniejo.Tournaments.UnitTests.Tournaments
{
    public sealed class UpdateTournamentPublicSettingsCommandValidatorTests
    {
        private readonly UpdateTournamentPublicSettingsCommandValidator _validator = new();

        [Fact]
        public void Validate_ShouldSucceed_WhenTournamentIdIsProvided()
        {
            var command = new UpdateTournamentPublicSettingsCommand(Guid.NewGuid(), true, true, true, true, true, true, true);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldFail_WhenTournamentIdIsEmpty()
        {
            var command = new UpdateTournamentPublicSettingsCommand(Guid.Empty, true, true, true, true, true, true, true);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTournamentPublicSettingsCommand.TournamentId));
        }
    }
}
