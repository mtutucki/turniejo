using FluentAssertions;
using Turniejo.Matches.Application.Matches.CreateBracketMatch;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class CreateBracketMatchCommandValidatorTests
    {
        private readonly CreateBracketMatchCommandValidator _validator = new();

        [Fact]
        public void Validate_ShouldSucceed_WhenCommandIsValid()
        {
            var command = new CreateBracketMatchCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StageType.Knockout);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldFail_WhenHomeAndAwayTeamsAreTheSame()
        {
            var teamId = Guid.NewGuid();
            var command = new CreateBracketMatchCommand(
                Guid.NewGuid(), Guid.NewGuid(), teamId, teamId, StageType.Knockout);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBracketMatchCommand.AwayTournamentTeamId));
        }

        [Fact]
        public void Validate_ShouldFail_WhenStageTypeIsInvalid()
        {
            var command = new CreateBracketMatchCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (StageType)999);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBracketMatchCommand.StageType));
        }
    }
}
