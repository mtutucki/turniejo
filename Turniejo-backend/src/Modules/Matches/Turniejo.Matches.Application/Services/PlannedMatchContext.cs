using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Services
{
    // Mecz kolejnej rundy pucharu, który jeszcze nie powstał (czeka na drużyny), istnieje tylko jako
    // planowany termin na węźle drabinki. Organizator może mu z góry zmienić termin i wskazać sędziego.
    // Wspólne wczytanie i sprawdzenie węzła oraz kolizji dla obu komend.
    public sealed class PlannedMatchContext(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        IBracketModule bracketModule)
    {
        public sealed record Loaded(
            BracketNodeInfoDto Node,
            IReadOnlyList<BracketNodeInfoDto> Nodes,
            IReadOnlyList<Match> Matches,
            TournamentDto Tournament,
            TimeSpan Duration,
            TimeSpan Slot);

        public async Task<Result<Loaded>> LoadAsync(Guid tournamentId, Guid nodeId, CancellationToken cancellationToken)
        {
            var nodes = await bracketModule.GetNodesByTournamentIdAsync(tournamentId, cancellationToken);
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
                return Result.Failure<Loaded>(Error.NotFound("BracketNode", nodeId));
            if (node.MatchId.HasValue)
                return Result.Failure<Loaded>(Error.Conflict("BracketNode", "This match already exists, change it in the schedule"));
            if (node.HasBye)
                return Result.Failure<Loaded>(Error.Validation("BracketNode", "A pair with a bye has no match"));

            var tournament = await tournamentModule.GetTournamentAsync(tournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<Loaded>(Error.NotFound("Tournament", tournamentId));
            if (tournament.Status is "Finished" or "Cancelled")
                return Result.Failure<Loaded>(Error.Conflict("Status", "The tournament has finished or been cancelled"));

            var settings = await tournamentModule.GetTournamentSettingsAsync(tournamentId, cancellationToken);
            var duration = TimeSpan.FromMinutes(Math.Max(settings?.MatchDurationMinutes ?? 1, 1));
            var slot = duration + TimeSpan.FromMinutes(Math.Max(settings?.BreakDurationMinutes ?? 0, 0));

            var matches = (await dbContext.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken))
                .Where(m => m.Status != MatchStatus.Cancelled)
                .ToList();

            return Result.Success(new Loaded(node, nodes, matches, tournament, duration, slot));
        }

        /// <summary>Inny mecz lub planowany mecz tego sędziego nachodzi na [start, start + czas meczu).</summary>
        public static bool RefereeBusy(Loaded ctx, Guid refereeId, DateTimeOffset start)
        {
            var end = start + ctx.Duration;
            return ctx.Matches.Any(m => m.RefereeId == refereeId && m.ScheduledAt < end && start < m.ScheduledAt + ctx.Duration)
                || ctx.Nodes.Any(n => n.Id != ctx.Node.Id && !n.MatchId.HasValue && n.PlannedRefereeId == refereeId &&
                                      n.PlannedAt is { } at && at < end && start < at + ctx.Duration);
        }
    }
}
