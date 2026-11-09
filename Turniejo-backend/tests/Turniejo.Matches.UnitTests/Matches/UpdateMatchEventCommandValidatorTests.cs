using FluentAssertions;
using Turniejo.Matches.Application.Matches.UpdateMatchEvent;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    // QA v3: MNZ-017b - edycja minuty ma ten sam zakres co dodanie zdarzenia (0-300).
    public sealed class UpdateMatchEventCommandValidatorTests
    {
        private static UpdateMatchEventCommand WithMinute(int minute) =>
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, minute, null, null, null);

        [Theory]
        [InlineData(0)]
        [InlineData(300)]
        public void MNZ_017b_MinuteOnTheBoundary_IsValid(int minute) =>
            new UpdateMatchEventCommandValidator().Validate(WithMinute(minute)).IsValid.Should().BeTrue();

        [Theory]
        [InlineData(-1)]
        [InlineData(301)]
        public void MNZ_017b_MinuteOutsideTheRange_IsRejected(int minute) =>
            new UpdateMatchEventCommandValidator().Validate(WithMinute(minute)).IsValid.Should().BeFalse();
    }
}
