using FluentAssertions;
using Turniejo.Brackets.Application.Brackets.GenerateBracket;
using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class GenerateBracketCommandValidatorTests
    {
        private readonly GenerateBracketCommandValidator _validator = new();

        private static BracketPairingInput FixedPairing() =>
            new(BracketSourceType.Fixed, null, null, Guid.NewGuid(), BracketSourceType.Fixed, null, null, Guid.NewGuid());

        private static BracketPairingInput BothSidesByePairing() =>
            new(BracketSourceType.Bye, null, null, null, BracketSourceType.Bye, null, null, null);

        [Fact]
        public void Validate_ShouldFail_WhenPairingHasByeOnBothSides()
        {
            // Zamyka lukę z audytu: para bez żadnej drużyny po żadnej stronie nie ma kogo awansować.
            var command = new GenerateBracketCommand(
                Guid.NewGuid(), Guid.NewGuid(), [FixedPairing(), BothSidesByePairing()]);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Contains("Bye"));
        }

        [Fact]
        public void Validate_ShouldSucceed_WhenByeIsOnlyOnOneSide()
        {
            var oneSidedBye = new BracketPairingInput(
                BracketSourceType.Fixed, null, null, Guid.NewGuid(),
                BracketSourceType.Bye, null, null, null);
            var command = new GenerateBracketCommand(
                Guid.NewGuid(), Guid.NewGuid(), [FixedPairing(), oneSidedBye]);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }
    }
}
