using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Application.Schedule.GenerateSchedule
{
    /// <summary>
    /// Jeden mecz do ułożenia: mecz grupowy, istniejący mecz pucharu albo węzeł drabinki, który dopiero
    /// dostanie drużyny. Clubs to drużyny z bazy (TeamId) znane na pewno, PossibleClubs te, które mogą
    /// zagrać (puchar przed rozstrzygnięciem), razem z pewnymi. After: mecze, które muszą się skończyć
    /// wcześniej (poprzednia runda, faza grupowa grup źródłowych). Round: kolejka w grupie / runda pucharu.
    /// </summary>
    internal sealed record ScheduleItem(
        Guid Key,
        Guid CategoryId,
        IReadOnlySet<Guid> Clubs,
        IReadOnlySet<Guid> PossibleClubs,
        IReadOnlyList<Guid> After,
        int Round);

    /// <summary>Mecz, którego generator nie rusza: zajmuje boisko i drużyny w swoim czasie.</summary>
    internal sealed record FixedMatch(Guid? PitchId, DateTimeOffset StartAt, IReadOnlySet<Guid> Clubs);

    internal sealed record ScheduleSlot(Guid PitchId, DateTimeOffset StartAt);

    internal sealed record ScheduleResult(IReadOnlyDictionary<Guid, ScheduleSlot> Slots, int Unplaced);

    /// <summary>
    /// Układa mecze wszystkich kategorii razem na wspólnej siatce terminów (mecz + przerwa) w oknach dni.
    /// Reguła twarda: drużyna (ta sama drużyna z bazy także w innej kategorii) nigdy nie gra dwóch
    /// meczów naraz. Cel: terminarz kończy się jak najwcześniej (boisko nie stoi puste, gdy ktoś może
    /// grać), a w jego ramach drużyny mają jak najdłuższe przerwy. Kilka kolejności wyboru meczów daje
    /// kilka terminarzy; wygrywa ten, który kończy się najwcześniej, a przy remisie ten z lepszymi przerwami.
    /// </summary>
    internal static class ScheduleBuilder
    {
        private enum Priority { Rounds, RoundsWithRest, LongestRest, BalancedCategories }

        public static ScheduleResult Build(
            IReadOnlyList<ScheduleItem> items,
            IReadOnlyList<Guid> pitchIds,
            IReadOnlyList<FixedMatch> fixedMatches,
            IReadOnlyList<ScheduleWindow> windows,
            TournamentSettingsSummaryDto settings)
        {
            var slotStarts = SlotStarts(windows, settings).ToList();
            var matchDuration = TimeSpan.FromMinutes(settings.MatchDurationMinutes);
            var slotLength = TimeSpan.FromMinutes(settings.MatchDurationMinutes + settings.BreakDurationMinutes);

            // Najpierw ściśle: mecze pucharu, w których może zagrać ta sama drużyna z innej kategorii,
            // też się nie nakładają. Gdy tak się nie da zmieścić, liczą się tylko drużyny znane na pewno.
            Candidate? best = null;
            foreach (var strictPossible in new[] { true, false })
            {
                foreach (var priority in Enum.GetValues<Priority>())
                {
                    var candidate = Run(items, pitchIds, fixedMatches, windows, slotStarts, matchDuration, slotLength, priority, strictPossible);
                    if (best == null || candidate.IsBetterThan(best))
                        best = candidate;
                }

                if (best!.Unplaced == 0)
                    break;
            }

            return new ScheduleResult(best!.Slots, best.Unplaced);
        }

        /// <summary>
        /// Początki slotów (mecz + przerwa) we wszystkich oknach po kolei. Mecz musi się zmieścić w oknie
        /// w całości, nie tylko zacząć przed jego końcem.
        /// </summary>
        public static IEnumerable<(DateTimeOffset SlotStart, int WindowIndex)> SlotStarts(
            IReadOnlyList<ScheduleWindow> windows, TournamentSettingsSummaryDto settings)
        {
            var matchDuration = TimeSpan.FromMinutes(settings.MatchDurationMinutes);
            var slotMinutes = Math.Max(1, settings.MatchDurationMinutes + settings.BreakDurationMinutes);

            for (var i = 0; i < windows.Count; i++)
                for (var slotStart = windows[i].StartAt;
                     slotStart + matchDuration <= windows[i].EndAt;
                     slotStart = slotStart.AddMinutes(slotMinutes))
                    yield return (slotStart, i);
        }

        private static Candidate Run(
            IReadOnlyList<ScheduleItem> items,
            IReadOnlyList<Guid> pitchIds,
            IReadOnlyList<FixedMatch> fixedMatches,
            IReadOnlyList<ScheduleWindow> windows,
            List<(DateTimeOffset SlotStart, int WindowIndex)> slotStarts,
            TimeSpan matchDuration,
            TimeSpan slotLength,
            Priority priority,
            bool strictPossible)
        {
            var keys = items.Select(i => i.Key).ToHashSet();
            var order = items.Select((item, index) => (item.Key, index)).ToDictionary(x => x.Key, x => x.index);
            var totalInCategory = items.GroupBy(i => i.CategoryId).ToDictionary(g => g.Key, g => g.Count());
            var placedInCategory = new Dictionary<Guid, int>();

            var slots = new Dictionary<Guid, ScheduleSlot>();
            var readyAt = new Dictionary<Guid, DateTimeOffset>();
            var remaining = new List<ScheduleItem>(items);
            var placedInWindow = new int[windows.Count];
            // Ostatni mecz drużyny: koniec i okno (dzień). Nowy dzień = drużyna wypoczęta.
            var lastPlayed = new Dictionary<Guid, (DateTimeOffset End, int Window)>();

            foreach (var (slotStart, windowIndex) in slotStarts)
            {
                if (remaining.Count == 0)
                    break;

                var windowLimit = (windows[windowIndex].MaxMatches ?? int.MaxValue) - placedInWindow[windowIndex];
                if (windowLimit <= 0)
                    continue;

                var slotEnd = slotStart + matchDuration;
                var overlappingFixed = fixedMatches
                    .Where(f => f.StartAt < slotEnd && slotStart < f.StartAt + matchDuration)
                    .ToList();
                var freePitches = pitchIds
                    .Where(p => !overlappingFixed.Any(f => f.PitchId == p))
                    .ToList();
                var capacity = Math.Min(freePitches.Count, windowLimit);
                if (capacity == 0)
                    continue;

                var busyClubs = overlappingFixed.SelectMany(f => f.Clubs).ToHashSet();

                TimeSpan RestOf(ScheduleItem item)
                {
                    var rest = TimeSpan.MaxValue;
                    foreach (var club in item.PossibleClubs)
                    {
                        if (lastPlayed.TryGetValue(club, out var last) && last.Window == windowIndex && slotStart - last.End < rest)
                            rest = slotStart - last.End;
                    }

                    return rest;
                }

                var available = remaining
                    .Where(item => item.After.All(key => !keys.Contains(key) ||
                                                         (readyAt.TryGetValue(key, out var ready) && ready <= slotStart)))
                    .Where(item => !item.Clubs.Overlaps(busyClubs))
                    .Select(item => (Item: item, Gap: RestOf(item)))
                    .ToList();

                var ordered = priority switch
                {
                    Priority.Rounds => available
                        .OrderBy(x => x.Item.Round).ThenBy(x => order[x.Item.Key]),
                    // Jak dawniej: najpierw drużyny, które nie grały w poprzednim slocie.
                    Priority.RoundsWithRest => available
                        .OrderBy(x => x.Gap < slotLength ? 1 : 0).ThenBy(x => x.Item.Round).ThenBy(x => order[x.Item.Key]),
                    Priority.LongestRest => available
                        .OrderByDescending(x => x.Gap).ThenBy(x => x.Item.Round).ThenBy(x => order[x.Item.Key]),
                    _ => available
                        .OrderBy(x => (double)placedInCategory.GetValueOrDefault(x.Item.CategoryId) / totalInCategory[x.Item.CategoryId])
                        .ThenByDescending(x => x.Gap).ThenBy(x => x.Item.Round).ThenBy(x => order[x.Item.Key]),
                };

                var chosen = new List<ScheduleItem>();
                foreach (var (item, _) in ordered)
                {
                    if (chosen.Count >= capacity)
                        break;

                    if (chosen.Any(other => Conflicts(item, other, strictPossible)))
                        continue;

                    chosen.Add(item);
                }

                for (var i = 0; i < chosen.Count; i++)
                {
                    var item = chosen[i];
                    slots[item.Key] = new ScheduleSlot(freePitches[i], slotStart);
                    readyAt[item.Key] = slotStart + slotLength;
                    remaining.Remove(item);
                    placedInCategory[item.CategoryId] = placedInCategory.GetValueOrDefault(item.CategoryId) + 1;
                    foreach (var club in item.PossibleClubs)
                        lastPlayed[club] = (slotEnd, windowIndex);
                }

                placedInWindow[windowIndex] += chosen.Count;
            }

            return Candidate.Evaluate(items, slots, remaining.Count, slotStarts, slotLength);
        }

        // W jednej kategorii drabinka sama rozdziela drużyny (dwa mecze tej samej rundy nie mają wspólnych
        // drużyn), więc przypuszczenia liczą się tylko między kategoriami.
        private static bool Conflicts(ScheduleItem a, ScheduleItem b, bool strictPossible) =>
            a.Clubs.Overlaps(b.Clubs) ||
            (strictPossible && a.CategoryId != b.CategoryId && a.PossibleClubs.Overlaps(b.PossibleClubs));

        private sealed record Candidate(
            IReadOnlyDictionary<Guid, ScheduleSlot> Slots,
            int Unplaced,
            int LastSlotIndex,
            int SumOfDayEnds,
            int BackToBack,
            TimeSpan ShortestRest,
            double AverageRestSlots)
        {
            public static Candidate Evaluate(
                IReadOnlyList<ScheduleItem> items,
                Dictionary<Guid, ScheduleSlot> slots,
                int unplaced,
                List<(DateTimeOffset SlotStart, int WindowIndex)> slotStarts,
                TimeSpan slotLength)
            {
                var indexOf = slotStarts.Select((s, i) => (s.SlotStart, i)).ToDictionary(x => x.SlotStart, x => x.i);
                var windowOf = slotStarts.ToDictionary(s => s.SlotStart, s => s.WindowIndex);
                var placedIndexes = slots.Values.Select(s => indexOf[s.StartAt]).ToList();
                var lastSlot = placedIndexes.DefaultIfEmpty(-1).Max();
                var sumOfDayEnds = slots.Values
                    .GroupBy(s => windowOf[s.StartAt])
                    .Sum(g => g.Max(s => indexOf[s.StartAt]));

                // Przerwy liczone dla drużyn znanych na pewno, w obrębie jednego dnia.
                var backToBack = 0;
                var shortest = TimeSpan.MaxValue;
                var restSlotsSum = 0.0;
                var restCount = 0;
                var byClub = items
                    .Where(i => slots.ContainsKey(i.Key))
                    .SelectMany(i => i.Clubs.Select(c => (Club: c, Slot: slots[i.Key])))
                    .GroupBy(x => x.Club);
                foreach (var club in byClub)
                {
                    var starts = club.Select(x => x.Slot.StartAt).OrderBy(s => s).ToList();
                    for (var i = 1; i < starts.Count; i++)
                    {
                        if (windowOf[starts[i]] != windowOf[starts[i - 1]])
                            continue;

                        var gap = starts[i] - starts[i - 1];
                        if (gap <= slotLength)
                            backToBack++;
                        if (gap < shortest)
                            shortest = gap;
                        restSlotsSum += gap / slotLength;
                        restCount++;
                    }
                }

                return new Candidate(slots, unplaced, lastSlot, sumOfDayEnds, backToBack, shortest,
                    restCount == 0 ? 0 : restSlotsSum / restCount);
            }

            public bool IsBetterThan(Candidate other)
            {
                if (Unplaced != other.Unplaced)
                    return Unplaced < other.Unplaced;
                if (LastSlotIndex != other.LastSlotIndex)
                    return LastSlotIndex < other.LastSlotIndex;
                if (SumOfDayEnds != other.SumOfDayEnds)
                    return SumOfDayEnds < other.SumOfDayEnds;
                if (BackToBack != other.BackToBack)
                    return BackToBack < other.BackToBack;
                if (ShortestRest != other.ShortestRest)
                    return ShortestRest > other.ShortestRest;
                return AverageRestSlots > other.AverageRestSlots;
            }
        }
    }
}
