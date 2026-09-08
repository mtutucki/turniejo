using FluentAssertions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class RecalculateMatchScoreCommandValidatorTests
    {
        private readonly RecalculateMatchScoreCommandValidator _validator = new();

        [Fact]
        public void Validate_ShouldSucceed_WhenMatchIdIsProvided()
        {
            var result = _validator.Validate(new RecalculateMatchScoreCommand(Guid.NewGuid()));

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldFail_WhenMatchIdIsEmpty()
        {
            var result = _validator.Validate(new RecalculateMatchScoreCommand(Guid.Empty));

            result.IsValid.Should().BeFalse();
        }
    }
}
