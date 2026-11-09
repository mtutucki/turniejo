using Turniejo.Brackets.Domain.Entities;

namespace Turniejo.Brackets.Application.Brackets.SwapBracketSlots
{
    // Zamiana slotów może zamienić mecz kolejnej rundy z powrotem w zapowiedź (drużyna z wolnego losu
    // zaczyna zależeć od meczu). Zapowiedź zostawała w swoim dawnym terminie, nawet gdy wypadał on
    // równo z meczem, z którego przychodzi drużyna. Taki termin czyścimy - organizator ustawi nowy.
    internal static class PlannedSlotGuard
    {
        /// <param name="sourceStartsAt">Godzina meczu w danym węźle (null, gdy węzeł nie ma meczu z terminem).</param>
        public static void ClearPlansNotAfterSources(IReadOnlyCollection<BracketNode> nodes, Func<BracketNode, DateTimeOffset?> sourceStartsAt)
        {
            foreach (var target in nodes.Where(n => n.PlannedAt.HasValue && !n.MatchId.HasValue))
            {
                var sourcesStart = nodes
                    .Where(n => n.WinnerGoesToNodeId == target.Id || n.LoserGoesToNodeId == target.Id)
                    .Select(sourceStartsAt)
                    .OfType<DateTimeOffset>()
                    .ToList();

                if (sourcesStart.Count > 0 && sourcesStart.Max() >= target.PlannedAt!.Value)
                    target.ClearPlannedSlot();
            }
        }
    }
}
