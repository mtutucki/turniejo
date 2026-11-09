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
    public sealed class ScheduleConflictChecker(IMatchesDbContext dbContext, ITournamentModule tournamentModule, ITeamModule teamModule)
    {
        /// <summary>Nowy termin/boisko meczu: boisko z turnieju i aktywne, w datach turnieju, bez nakładek.</summary>
        public async Task<Error?> FindConflictAsync(
            Match match,
            Guid? pitchId,
            DateTimeOffset scheduledAt,
            string pitchErrorField,
            CancellationToken cancellationToken)
        {
            // Obecne boisko meczu zostawiamy w spokoju, nawet jeśli potem je dezaktywowano — inaczej samej
            // godziny nie dałoby się zmienić bez przenoszenia meczu.
            if (pitchId.HasValue && pitchId != match.PitchId)
            {
                var pitch = await dbContext.GetPitchByIdAsync(pitchId.Value, cancellationToken);
                if (pitch == null || pitch.TournamentId != match.TournamentId)
                    return Error.Validation(pitchErrorField, "Pitch does not belong to this tournament");
                if (!pitch.IsActive)
                    return Error.Validation(pitchErrorField, "Pitch is inactive");
            }

            var duration = await GetMatchDurationAsync(match.TournamentId, cancellationToken);

            var tournament = await tournamentModule.GetTournamentAsync(match.TournamentId, cancellationToken);
            if (tournament?.StartAt is { } startAt && tournament.EndAt is { } endAt &&
                (scheduledAt < startAt || scheduledAt + duration > endAt))
                return Error.Validation("ScheduledAt", "Match must take place within the tournament dates");

            var others = await GetOverlappingAsync(match, scheduledAt, duration, cancellationToken);

            if (pitchId.HasValue && others.Any(m => m.PitchId == pitchId))
                return Error.Validation(pitchErrorField, "Another match is already scheduled on this pitch at this time");

            // Ta sama drużyna w dwóch kategoriach ma dwa zgłoszenia - porównujemy klub (TeamId), tak jak generator.
            var entries = others.Count == 0
                ? []
                : await teamModule.GetTournamentTeamsByTournamentIdAsync(match.TournamentId, cancellationToken);
            var clubByEntry = entries.ToDictionary(t => t.Id, t => t.TeamId);
            Guid Club(Guid entry) => clubByEntry.GetValueOrDefault(entry, entry);
            var clubs = new[] { Club(match.HomeTournamentTeamId), Club(match.AwayTournamentTeamId) };
            if (others.Any(m => clubs.Contains(Club(m.HomeTournamentTeamId)) || clubs.Contains(Club(m.AwayTournamentTeamId))))
                return Error.Validation("ScheduledAt", "One of the teams already plays another match at this time");

            if (match.RefereeId.HasValue && others.Any(m => m.RefereeId == match.RefereeId))
                return Error.Validation("ScheduledAt", "The referee already has another match at this time");

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
