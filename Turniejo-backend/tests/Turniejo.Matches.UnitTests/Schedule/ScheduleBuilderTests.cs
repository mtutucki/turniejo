using FluentAssertions;
using Turniejo.Matches.Application.Schedule.GenerateSchedule;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class ScheduleBuilderTests
    {
        private static readonly DateTimeOffset Start = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        private static readonly TournamentSettingsSummaryDto Settings = new(Guid.NewGuid(), 30, 10, 60, 3, 1, 0, false, false, 3, 0);
        private static readonly Guid CategoryA = Guid.NewGuid();
        private static readonly Guid CategoryB = Guid.NewGuid();

        private static ScheduleItem Item(Guid category, Guid[] clubs, Guid[]? possible = null, Guid[]? after = null, int round = 0) =>
            new(Guid.NewGuid(), category, clubs.ToHashSet(), (possible ?? clubs).Concat(clubs).ToHashSet(), after ?? [], round);

        private static ScheduleResult Build(IReadOnlyList<ScheduleItem> items, int pitches, int hours = 4, int? maxMatches = null) =>
            ScheduleBuilder.Build(items, Enumerable.Range(0, pitches).Select(_ => Guid.NewGuid()).ToList(), [],
                [new ScheduleWindow(Start, Start.AddHours(hours), maxMatches)], Settings);

        [Fact]
        public void LaterRound_StartsOnlyAfterTheMatchesItDependsOn()
        {
            var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var semi1 = Item(CategoryA, [a, b]);
            var semi2 = Item(CategoryA, [c, d]);
            var final = Item(CategoryA, [], possible: [a, b, c, d], after: [semi1.Key, semi2.Key], round: 1);

            var result = Build([final, semi1, semi2], pitches: 2);

            result.Unplaced.Should().Be(0);
            result.Slots[final.Key].StartAt.Should().Be(Start.AddMinutes(40));
        }

        [Fact]
        public void PossibleOpponentFromAnotherCategory_IsNotPlannedAtTheSameTime_WhenThereIsRoom()
        {
            // Klub X może dojść do finału w obu kategoriach — finały nie powinny wypaść naraz.
            var x = Guid.NewGuid();
            var finalA = Item(CategoryA, [], possible: [x, Guid.NewGuid()]);
            var finalB = Item(CategoryB, [], possible: [x, Guid.NewGuid()]);

            var result = Build([finalA, finalB], pitches: 2);

            result.Slots[finalA.Key].StartAt.Should().NotBe(result.Slots[finalB.Key].StartAt);
        }

        [Fact]
        public void PossibleOpponents_GiveWay_WhenSeparatingThemWouldNotFit()
        {
            // Jeden termin na dwóch boiskach: rozdzielenie się nie mieści, więc liczą się tylko pewne drużyny.
            var x = Guid.NewGuid();
            var finalA = Item(CategoryA, [], possible: [x, Guid.NewGuid()]);
            var finalB = Item(CategoryB, [], possible: [x, Guid.NewGuid()]);

            var result = Build([finalA, finalB], pitches: 2, hours: 1);

            result.Unplaced.Should().Be(0);
        }

        [Fact]
        public void SameClub_NeverPlaysTwoMatchesInOneSlot_EvenWhenItMakesTheScheduleLonger()
        {
            var x = Guid.NewGuid();
            var items = new[]
            {
                Item(CategoryA, [x, Guid.NewGuid()]),
                Item(CategoryB, [x, Guid.NewGuid()]),
            };

            var result = Build(items, pitches: 2, hours: 1);

            result.Unplaced.Should().Be(1);
        }

        [Fact]
        public void TeamsGetARest_WithoutMakingTheDayLonger()
        {
            // Jedno boisko, dwie kategorie po 3 drużyny: 6 meczów w 6 terminach. Kolejka po kolejce w jednej
            // kategorii dawałaby mecze z rzędu; na zmianę z drugą kategorią nikt nie gra dwa razy pod rząd.
            static ScheduleItem[] RoundRobinOfThree(Guid category)
            {
                var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
                return [Item(category, [a, b]), Item(category, [a, c], round: 1), Item(category, [b, c], round: 2)];
            }

            var items = RoundRobinOfThree(CategoryA).Concat(RoundRobinOfThree(CategoryB)).ToList();

            var result = Build(items, pitches: 1);

            result.Unplaced.Should().Be(0);
            result.Slots.Values.Max(s => s.StartAt).Should().Be(Start.AddMinutes(5 * 40));
            foreach (var club in items.SelectMany(i => i.Clubs).Distinct())
            {
                var starts = items.Where(i => i.Clubs.Contains(club)).Select(i => result.Slots[i.Key].StartAt).OrderBy(s => s).ToList();
                starts.Zip(starts.Skip(1)).Should().OnlyContain(p => p.Second - p.First > TimeSpan.FromMinutes(40));
            }
        }

        private static ScheduleItem GroupMatch(Guid groupId) =>
            new(Guid.NewGuid(), CategoryA, new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() },
                new HashSet<Guid>(), [], 0, groupId);

        private static ScheduleResult BuildOnPitches(
            IReadOnlyList<ScheduleItem> items, Dictionary<Guid, IReadOnlySet<Guid>> pitchGroups, TimeSpan length) =>
            ScheduleBuilder.Build(items, pitchGroups.Keys.ToList(), [],
                [new ScheduleWindow(Start, Start + length)], Settings, pitchGroups);

        [Fact]
        public void GroupMatches_ArePlacedOnlyOnPitchesAssignedToTheirGroup()
        {
            // Dwie hale: boiska A i B dla grupy 1, C i D dla grupy 2.
            var (group1, group2) = (Guid.NewGuid(), Guid.NewGuid());
            var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var pitchGroups = new Dictionary<Guid, IReadOnlySet<Guid>>
            {
                [a] = new HashSet<Guid> { group1 }, [b] = new HashSet<Guid> { group1 },
                [c] = new HashSet<Guid> { group2 }, [d] = new HashSet<Guid> { group2 },
            };
            var first = Enumerable.Range(0, 6).Select(_ => GroupMatch(group1)).ToList();
            var second = Enumerable.Range(0, 6).Select(_ => GroupMatch(group2)).ToList();

            var result = BuildOnPitches([.. first, .. second], pitchGroups, TimeSpan.FromHours(4));

            result.Unplaced.Should().Be(0);
            first.Select(m => result.Slots[m.Key].PitchId).Should().OnlyContain(p => p == a || p == b);
            second.Select(m => result.Slots[m.Key].PitchId).Should().OnlyContain(p => p == c || p == d);
        }

        [Fact]
        public void MatchWithoutGroup_CanPlayOnAnyPitch()
        {
            // Puchar nie ma grupy: gra także na boiskach przypisanych do grup.
            var group1 = Guid.NewGuid();
            var pitch = Guid.NewGuid();
            var final = Item(CategoryA, [Guid.NewGuid(), Guid.NewGuid()]);

            var result = BuildOnPitches([final], new() { [pitch] = new HashSet<Guid> { group1 } }, TimeSpan.FromHours(1));

            result.Unplaced.Should().Be(0);
            result.Slots[final.Key].PitchId.Should().Be(pitch);
        }

        [Fact]
        public void PitchWithoutGroups_TakesMatchesOfAnyGroup()
        {
            var group1 = Guid.NewGuid();
            var pitch = Guid.NewGuid();
            var match = GroupMatch(group1);

            var result = BuildOnPitches([match], new() { [pitch] = new HashSet<Guid>() }, TimeSpan.FromHours(1));

            result.Slots[match.Key].PitchId.Should().Be(pitch);
        }

        [Fact]
        public void MatchesInOneSlot_ArePairedWithPitchesSoThatAllFit()
        {
            // Jeden termin: boisko "wolne" przyjmuje każdą grupę, boisko grupy 1 tylko ją. Mecz grupy 1
            // wybrany pierwszy bierze pierwsze boisko ("wolne"), a mecz grupy 2 może grać tylko tam,
            // więc mecz grupy 1 przechodzi na swoje boisko.
            var (group1, group2) = (Guid.NewGuid(), Guid.NewGuid());
            var (open, onlyGroup1) = (Guid.NewGuid(), Guid.NewGuid());
            var match1 = GroupMatch(group1);
            var match2 = GroupMatch(group2);

            var result = BuildOnPitches([match1, match2],
                new() { [open] = new HashSet<Guid>(), [onlyGroup1] = new HashSet<Guid> { group1 } },
                TimeSpan.FromMinutes(30));

            result.Unplaced.Should().Be(0);
            result.Slots[match2.Key].PitchId.Should().Be(open);
            result.Slots[match1.Key].PitchId.Should().Be(onlyGroup1);
        }
    }
}
