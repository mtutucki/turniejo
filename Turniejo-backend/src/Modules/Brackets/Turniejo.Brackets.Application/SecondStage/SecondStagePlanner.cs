namespace Turniejo.Brackets.Application.SecondStage
{
    /// <summary>
    /// Zakresy miejsc (Grupa I: miejsca 1-2) zamienione na konkretne miejsca z prawdziwej liczby drużyn
    /// w grupach fazy 1. Tę samą propozycję liczy frontend (features/second-stage/split.ts).
    /// </summary>
    public static class SecondStagePlanner
    {
        public sealed record Group(Guid Id, int Order);
        public sealed record Range(Guid GroupId, int From, int To);
        public sealed record Seat(Guid GroupId, Guid SourceGroupId, int SourcePosition);

        /// <summary>
        /// Gotowy podział: miejsca 1..N (N = największa grupa fazy 1) po równo na kolejne przedziały,
        /// np. 4 drużyny i 2 grupy: 1-2 i 3-4. Grupa fazy 2, dla której nie starczy miejsc, nie dostaje zakresu.
        /// </summary>
        public static List<Range> DefaultRanges(IReadOnlyList<Group> secondStage, int largestGroup)
        {
            var groups = secondStage.OrderBy(g => g.Order).ToList();
            if (groups.Count == 0 || largestGroup < 1)
                return [];

            var perGroup = (int)Math.Ceiling(largestGroup / (double)groups.Count);
            var ranges = new List<Range>();
            for (var i = 0; i < groups.Count; i++)
            {
                var from = i * perGroup + 1;
                if (from > largestGroup)
                    break;
                ranges.Add(new Range(groups[i].Id, from, Math.Min((i + 1) * perGroup, largestGroup)));
            }

            return ranges;
        }

        /// <summary>
        /// W grupie fazy 2 najpierw wszystkie miejsca o niższym numerze (1A, 1B…, potem 2A, 2B…).
        /// Grupa fazy 1, która ma mniej drużyn niż numer miejsca, tego miejsca nie daje.
        /// </summary>
        public static List<Seat> SeatsFromRanges(
            IReadOnlyList<Range> ranges, IReadOnlyList<Group> secondStage, IReadOnlyList<Group> firstStage,
            Func<Guid, int> teamsInGroup)
        {
            var order = secondStage.ToDictionary(g => g.Id, g => g.Order);
            var sources = firstStage.OrderBy(g => g.Order).ToList();
            return ranges
                .OrderBy(r => order.GetValueOrDefault(r.GroupId))
                .SelectMany(r => Enumerable.Range(r.From, r.To - r.From + 1)
                    .SelectMany(position => sources
                        .Where(source => position <= teamsInGroup(source.Id))
                        .Select(source => new Seat(r.GroupId, source.Id, position))))
                .ToList();
        }

        /// <summary>Zakresy jednej kategorii nie mogą się nakładać - to samo miejsce trafiłoby do dwóch grup.</summary>
        public static bool Overlap(IReadOnlyList<Range> ranges) =>
            ranges.SelectMany(r => Enumerable.Range(r.From, r.To - r.From + 1)).GroupBy(p => p).Any(g => g.Count() > 1);
    }
}
