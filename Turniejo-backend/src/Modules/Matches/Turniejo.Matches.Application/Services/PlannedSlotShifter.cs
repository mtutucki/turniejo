using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Application.Services
{
    // Mecze kolejnych rund pucharu, które jeszcze nie istnieją, mają tylko planowany termin na węźle
    // drabinki — ScheduleConflictChecker ich nie widzi. Po ręcznym przesunięciu półfinału na 20:42 finał
    // zostawał w planie na 20:43, na tym samym boisku. Po każdej zmianie terminu planowane mecze są
    // odsuwane: najwcześniej po meczach, z których przyjdą drużyny, i bez nakładki na boisku. Tylko
    // w przód — prawdziwe mecze mają pierwszeństwo, a planowany termin nigdy nie robi się wcześniejszy.
    public sealed class PlannedSlotShifter(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        IBracketModule bracketModule)
    {
        public async Task ShiftAsync(Guid tournamentId, CancellationToken cancellationToken)
        {
            var nodes = await bracketModule.GetNodesByTournamentIdAsync(tournamentId, cancellationToken);
            var plannedNodes = nodes
                .Where(n => !n.MatchId.HasValue && n.PlannedAt.HasValue && n.PlannedPitchId.HasValue)
                .OrderBy(n => n.RoundNumber)
                .ThenByDescending(n => n.IsThirdPlace)
                .ToList();
            if (plannedNodes.Count == 0)
                return;

            var settings = await tournamentModule.GetTournamentSettingsAsync(tournamentId, cancellationToken);
            var duration = TimeSpan.FromMinutes(Math.Max(settings?.MatchDurationMinutes ?? 1, 1));
            var slot = duration + TimeSpan.FromMinutes(Math.Max(settings?.BreakDurationMinutes ?? 0, 0));

            var matches = (await dbContext.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken))
                .Where(m => m.Status != MatchStatus.Cancelled)
                .ToList();
            var matchStart = matches.ToDictionary(m => m.Id, m => m.ScheduledAt);

            // Zajęte terminy na boiskach: prawdziwe mecze, a potem kolejno ustalane planowane.
            var busy = matches
                .Where(m => m.PitchId.HasValue)
                .Select(m => (PitchId: m.PitchId!.Value, m.ScheduledAt))
                .ToList();

            var readyAt = new Dictionary<Guid, DateTimeOffset>();
            foreach (var node in nodes)
            {
                if (node.MatchId.HasValue && matchStart.TryGetValue(node.MatchId.Value, out var start))
                    readyAt[node.Id] = start + slot;
            }

            var shifted = new List<BracketPlannedSlotDto>();
            foreach (var node in plannedNodes)
            {
                var pitchId = node.PlannedPitchId!.Value;
                var start = new[] { node.HomeSourceNodeId, node.AwaySourceNodeId }
                    .Where(id => id.HasValue && readyAt.ContainsKey(id.Value))
                    .Select(id => readyAt[id!.Value])
                    .Append(node.PlannedAt!.Value)
                    .Max();

                // Każde przesunięcie idzie za koniec kolidującego meczu, więc pętla kończy się po
                // najwyżej tylu krokach, ile jest zajętych terminów.
                while (true)
                {
                    var blockers = busy
                        .Where(b => b.PitchId == pitchId && start < b.ScheduledAt + slot && b.ScheduledAt < start + duration)
                        .ToList();
                    if (blockers.Count == 0)
                        break;

                    start = blockers.Max(b => b.ScheduledAt) + slot;
                }

                busy.Add((pitchId, start));
                readyAt[node.Id] = start + slot;

                if (start != node.PlannedAt.Value)
                    shifted.Add(new BracketPlannedSlotDto(node.Id, pitchId, start));
            }

            await bracketModule.SetPlannedSlotsAsync(shifted, cancellationToken);
        }
    }
}
