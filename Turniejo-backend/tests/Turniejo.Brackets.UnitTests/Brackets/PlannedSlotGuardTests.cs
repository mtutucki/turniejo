using FluentAssertions;
using Turniejo.Brackets.Application.Brackets.SwapBracketSlots;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    // QA v3: PUC-031 - po zamianie slotu z wolnym losem półfinał stał się zapowiedzią, ale zachował
    // swój dawny termin, równy godzinie ćwierćfinału, na którego zwycięzcę czeka.
    public sealed class PlannedSlotGuardTests
    {
        private static readonly DateTimeOffset At8 = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

        private static (BracketNode Source, BracketNode Target) Pair(DateTimeOffset targetPlannedAt)
        {
            var roundId = Guid.NewGuid();
            var source = BracketNode.Create(Guid.NewGuid(), roundId,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;
            var target = BracketNode.Create(Guid.NewGuid(), Guid.NewGuid(),
                BracketSourceType.MatchWinner, null, null, source.Id, null,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;
            source.WireAdvancement(target.Id, BracketSlot.Home, null, null);
            target.PlanSlot(Guid.NewGuid(), targetPlannedAt);
            return (source, target);
        }

        [Fact]
        public void PUC_031_PreviewAtTheSameTimeAsItsSourceMatch_LosesItsPlannedTime()
        {
            var (source, target) = Pair(At8);

            PlannedSlotGuard.ClearPlansNotAfterSources([source, target], _ => At8);

            target.PlannedAt.Should().BeNull();
        }

        [Fact]
        public void PreviewAfterItsSourceMatch_KeepsItsPlannedTime()
        {
            var (source, target) = Pair(At8.AddMinutes(30));

            PlannedSlotGuard.ClearPlansNotAfterSources([source, target], _ => At8);

            target.PlannedAt.Should().Be(At8.AddMinutes(30));
        }
    }
}
