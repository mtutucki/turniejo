using FluentAssertions;
using Turniejo.Matches.Application.Pitches.DeletePitch;

namespace Turniejo.Matches.UnitTests.Pitches
{
    public sealed class DeletePitchCommandValidatorTests
    {
        private readonly DeletePitchCommandValidator _validator = new();

        [Fact]
        public void Validate_ShouldSucceed_WhenIdsAreProvided()
        {
            var command = new DeletePitchCommand(Guid.NewGuid(), Guid.NewGuid());

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldFail_WhenTournamentIdIsEmpty()
        {
            var command = new DeletePitchCommand(Guid.Empty, Guid.NewGuid());

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(DeletePitchCommand.TournamentId));
        }

        [Fact]
        public void Validate_ShouldFail_WhenPitchIdIsEmpty()
        {
            var command = new DeletePitchCommand(Guid.NewGuid(), Guid.Empty);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(DeletePitchCommand.PitchId));
        }
    }
}
