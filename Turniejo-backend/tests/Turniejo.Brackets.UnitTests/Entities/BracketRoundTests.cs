using FluentAssertions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.UnitTests.Entities
{
    public sealed class BracketRoundTests
    {
        [Fact]
        public void Create_ShouldSucceed_WhenDataIsValid()
        {
            var bracketId = Guid.NewGuid();

            var result = BracketRound.Create(Guid.NewGuid(), bracketId, 1, BracketRoundType.Standard, "Round 1");

            result.IsSuccess.Should().BeTrue();
            result.Value.BracketId.Should().Be(bracketId);
            result.Value.RoundNumber.Should().Be(1);
            result.Value.RoundType.Should().Be(BracketRoundType.Standard);
        }

        [Fact]
        public void Create_ShouldFail_WhenRoundNumberIsNotPositive()
        {
            var result = BracketRound.Create(Guid.NewGuid(), Guid.NewGuid(), 0, BracketRoundType.Standard, "Round 1");
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.RoundNumber");
        }

        [Fact]
        public void Create_ShouldFail_WhenNameIsEmpty()
        {
            var result = BracketRound.Create(Guid.NewGuid(), Guid.NewGuid(), 1, BracketRoundType.Standard, "");
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
        }
    }
}
