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
    }
}
