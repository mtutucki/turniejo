using FluentAssertions;
using Turniejo.Brackets.Application.SecondStage;
using static Turniejo.Brackets.Application.SecondStage.SecondStagePlanner;
using Range = Turniejo.Brackets.Application.SecondStage.SecondStagePlanner.Range;

namespace Turniejo.Brackets.UnitTests.Services
{
    public sealed class SecondStagePlannerTests
    {
        private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid();
        private static readonly Guid I = Guid.NewGuid(), II = Guid.NewGuid();
        private static readonly List<Group> FirstStage = [new(A, 1), new(B, 2), new(C, 3), new(D, 4)];
        private static readonly List<Group> SecondStage = [new(I, 5), new(II, 6)];

        [Fact]
        public void FAZ_025_DefaultRanges_SplitPlacesEvenly()
        {
            DefaultRanges(SecondStage, 4).Should().Equal(new Range(I, 1, 2), new Range(II, 3, 4));
            DefaultRanges(SecondStage, 3).Should().Equal(new Range(I, 1, 2), new Range(II, 3, 3));
            DefaultRanges(SecondStage, 1).Should().Equal(new Range(I, 1, 1));
            DefaultRanges(SecondStage, 0).Should().BeEmpty();
        }

        [Fact]
        public void FAZ_025_SeatsFromRanges_UseTheRealTeamCount_LowerPlacesFirst()
        {
            var teams = new Dictionary<Guid, int> { [A] = 3, [B] = 3, [C] = 2, [D] = 0 };

            var seats = SeatsFromRanges([new Range(II, 3, 4), new Range(I, 1, 2)], SecondStage, FirstStage, g => teams[g]);

            seats.Where(s => s.GroupId == I).Select(s => (s.SourcePosition, s.SourceGroupId))
                .Should().Equal((1, A), (1, B), (1, C), (2, A), (2, B), (2, C));
            seats.Where(s => s.GroupId == II).Select(s => (s.SourcePosition, s.SourceGroupId))
                .Should().Equal((3, A), (3, B));
            seats.First().GroupId.Should().Be(I, "grupy fazy 2 w swojej kolejności");
        }

        [Fact]
        public void FAZ_027_Overlap_FindsAPlaceInTwoRanges()
        {
            Overlap([new Range(I, 1, 2), new Range(II, 3, 4)]).Should().BeFalse();
            Overlap([new Range(I, 1, 2), new Range(II, 2, 4)]).Should().BeTrue();
        }
    }
}
