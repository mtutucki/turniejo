using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Services
{
    // Sam unikalny indeks (PitchId, ScheduledAt) łapie tylko identyczną godzinę startu — mecz przesunięty
    // o 10 minut na zajęte boisko, drużyna grająca jednocześnie na dwóch boiskach albo sędzia z dwoma
    // meczami naraz przechodziły bez błędu. Konflikt liczymy na przedziałach [start, start + czas meczu)
    // z TournamentSettings.
    public sealed record ScheduleMove(Match Match, Guid? PitchId, DateTimeOffset ScheduledAt);

    public sealed class ScheduleConflictChecker(IMatchesDbContext dbContext, ITournamentModule tournamentModule, ITeamModule teamModule)
    {
        /// <summary>Nowy termin/boisko meczu: boisko z turnieju i aktywne, w datach turnieju, bez nakładek.</summary>
        public Task<Error?> FindConflictAsync(
            Match match,
            Guid? pitchId,
            DateTimeOffset scheduledAt,
            string pitchErrorField,
            CancellationToken cancellationToken) =>
            FindConflictAsync([new ScheduleMove(match, pitchId, scheduledAt)], pitchErrorField, cancellationToken);

        /// <summary>
        /// Kilka meczów zmienia termin naraz (np. opóźnienie przesuwa kolejne mecze na boisku). Każdy
        /// sprawdzamy względem nowych terminów pozostałych - przesuwane mecze nie kolidują same ze sobą
        /// tylko dlatego, że na starej pozycji stoi jeszcze następny.
        /// </summary>
        public async Task<Error?> FindConflictAsync(
            IReadOnlyList<ScheduleMove> moves,
            string pitchErrorField,
            CancellationToken cancellationToken)
        {
            if (moves.Count == 0)
                return null;

            var tournamentId = moves[0].Match.TournamentId;

            foreach (var move in moves)
            {
                // Obecne boisko meczu zostawiamy w spokoju, nawet jeśli potem je dezaktywowano — inaczej samej
                // godziny nie dałoby się zmienić bez przenoszenia meczu.
                if (move.PitchId.HasValue && move.PitchId != move.Match.PitchId)
                {
                    var pitch = await dbContext.GetPitchByIdAsync(move.PitchId.Value, cancellationToken);
                    if (pitch == null || pitch.TournamentId != tournamentId)
                        return Error.Validation(pitchErrorField, "Pitch does not belong to this tournament");
                    if (!pitch.IsActive)
                        return Error.Validation(pitchErrorField, "Pitch is inactive");
                }
            }

            var duration = await GetMatchDurationAsync(tournamentId, cancellationToken);

            var tournament = await tournamentModule.GetTournamentAsync(tournamentId, cancellationToken);
            if (tournament?.StartAt is { } startAt && tournament.EndAt is { } endAt &&
                moves.Any(m => m.ScheduledAt < startAt || m.ScheduledAt + duration > endAt))
                return Error.Validation("ScheduledAt", "Match must take place within the tournament dates");

            var moved = moves.ToDictionary(m => m.Match.Id);
            var slots = (await dbContext.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken))
                .Where(m => !moved.ContainsKey(m.Id) && m.Status != MatchStatus.Cancelled)
                .Select(m => new ScheduleMove(m, m.PitchId, m.ScheduledAt))
                .Concat(moves)
                .ToList();

            Dictionary<Guid, Guid>? clubByEntry = null;
            foreach (var move in moves)
            {
                var end = move.ScheduledAt + duration;
                var others = slots
                    .Where(s => s.Match.Id != move.Match.Id)
                    .Where(s => s.ScheduledAt < end && move.ScheduledAt < s.ScheduledAt + duration)
                    .ToList();
                if (others.Count == 0)
                    continue;

                if (move.PitchId.HasValue && others.Any(s => s.PitchId == move.PitchId))
                    return Error.Validation(pitchErrorField, "Another match is already scheduled on this pitch at this time");

                // Ta sama drużyna w dwóch kategoriach ma dwa zgłoszenia - porównujemy klub (TeamId), tak jak generator.
                clubByEntry ??= (await teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, cancellationToken))
                    .ToDictionary(t => t.Id, t => t.TeamId);
                Guid Club(Guid entry) => clubByEntry.GetValueOrDefault(entry, entry);
                var clubs = new[] { Club(move.Match.HomeTournamentTeamId), Club(move.Match.AwayTournamentTeamId) };
                if (others.Any(s => clubs.Contains(Club(s.Match.HomeTournamentTeamId)) || clubs.Contains(Club(s.Match.AwayTournamentTeamId))))
                    return Error.Validation("ScheduledAt", "One of the teams already plays another match at this time");

                if (move.Match.RefereeId.HasValue && others.Any(s => s.Match.RefereeId == move.Match.RefereeId))
                    return Error.Validation("ScheduledAt", "The referee already has another match at this time");
            }

            return null;
        }

        /// <summary>Przypisanie sędziego do meczu w jego obecnym terminie.</summary>
        public async Task<Error?> FindRefereeConflictAsync(Match match, Guid refereeId, CancellationToken cancellationToken)
        {
            var duration = await GetMatchDurationAsync(match.TournamentId, cancellationToken);
            var others = await GetOverlappingAsync(match, match.ScheduledAt, duration, cancellationToken);

            return others.Any(m => m.RefereeId == refereeId)
                ? Error.Validation("RefereeId", "The referee already has another match at this time")
                : null;
        }

        private async Task<TimeSpan> GetMatchDurationAsync(Guid tournamentId, CancellationToken cancellationToken)
        {
            var settings = await tournamentModule.GetTournamentSettingsAsync(tournamentId, cancellationToken);
            return TimeSpan.FromMinutes(Math.Max(settings?.MatchDurationMinutes ?? 1, 1));
        }

        private async Task<List<Match>> GetOverlappingAsync(Match match, DateTimeOffset scheduledAt, TimeSpan duration, CancellationToken cancellationToken)
        {
            var end = scheduledAt + duration;
            return (await dbContext.GetMatchesByTournamentIdAsync(match.TournamentId, cancellationToken))
                .Where(m => m.Id != match.Id && m.Status != MatchStatus.Cancelled)
                .Where(m => m.ScheduledAt < end && scheduledAt < m.ScheduledAt + duration)
                .ToList();
        }
    }
}
