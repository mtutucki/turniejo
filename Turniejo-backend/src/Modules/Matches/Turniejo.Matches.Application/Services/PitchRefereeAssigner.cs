using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.Application.Services
{
    /// <summary>Mecz i sędzia jego boiska; PreviousPitchRefereeId: sędzia, którego ten mecz mógł dostać z boiska wcześniej.</summary>
    public sealed record PitchRefereeTarget(Match Match, Guid? PitchRefereeId, Guid? PreviousPitchRefereeId = null);

    /// <summary>
    /// Sędzia przypisany do boiska prowadzi jego mecze. Dostaje je nierozpoczęty mecz bez sędziego albo z
    /// dotychczasowym sędzią boiska (zmiana sędziego boiska, przeniesienie meczu z innego boiska). Mecz z
    /// ręcznie wybranym innym sędzią zostaje bez zmian, tak samo mecz, w którego czasie sędzia boiska ma
    /// już inny mecz. Mecze muszą być śledzone; zapis robi wywołujący.
    /// </summary>
    public sealed class PitchRefereeAssigner(
        IMatchesDbContext dbContext,
        IUserModule userModule,
        IAuthorizationService authorizationService,
        ITournamentModule tournamentModule)
    {
        /// <returns>Ile meczów zmieniło sędziego.</returns>
        public async Task<int> ApplyAsync(IReadOnlyList<PitchRefereeTarget> targets, Guid changedByUserId, CancellationToken cancellationToken)
        {
            var candidates = targets
                .Where(t => t.Match.Status is MatchStatus.Scheduled or MatchStatus.Ready or MatchStatus.Postponed)
                .Where(t => t.Match.RefereeId != t.PitchRefereeId)
                .Where(t => t.Match.RefereeId is null || t.Match.RefereeId == t.PreviousPitchRefereeId)
                .OrderBy(t => t.Match.ScheduledAt)
                .ToList();
            if (candidates.Count == 0)
                return 0;

            var tournamentId = candidates[0].Match.TournamentId;
            var settings = await tournamentModule.GetTournamentSettingsAsync(tournamentId, cancellationToken);
            var duration = TimeSpan.FromMinutes(Math.Max(settings?.MatchDurationMinutes ?? 1, 1));

            // Kto sędziuje kiedy: mecze z bazy, nadpisane bieżącym stanem przekazanych (także nowych) meczów.
            var busy = (await dbContext.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken))
                .Where(m => m.Status != MatchStatus.Cancelled)
                .ToDictionary(m => m.Id, m => (m.ScheduledAt, m.RefereeId));
            foreach (var target in targets)
                busy[target.Match.Id] = (target.Match.ScheduledAt, target.Match.RefereeId);

            var isReferee = new Dictionary<Guid, bool>();
            var changed = 0;
            foreach (var (match, refereeId, _) in candidates)
            {
                if (refereeId is { } id)
                {
                    if (!isReferee.TryGetValue(id, out var ok))
                        isReferee[id] = ok = await authorizationService.HasAnyRoleAsync(
                            id, nameof(RoleScopeType.Tournament), tournamentId, [nameof(UserRole.Referee)], cancellationToken);
                    if (!ok)
                        continue;

                    var end = match.ScheduledAt + duration;
                    if (busy.Any(b => b.Key != match.Id && b.Value.RefereeId == id &&
                                      b.Value.ScheduledAt < end && match.ScheduledAt < b.Value.ScheduledAt + duration))
                        continue;
                }

                var previous = match.RefereeId;
                if (match.AssignReferee(refereeId, changedByUserId).IsFailure)
                    continue;
                busy[match.Id] = (match.ScheduledAt, refereeId);
                changed++;

                // Rola sędziego w meczu idzie za przypisaniem (jak przy ręcznej zmianie sędziego).
                if (previous is { } old)
                    await userModule.RevokeRoleAsync(old, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, cancellationToken);
                if (refereeId is { } assigned)
                    await userModule.AssignRoleAsync(assigned, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, cancellationToken);

                var domainEvent = match.DomainEvents.OfType<RefereeAssignedToMatch>().Last();
                await dbContext.AddOutboxMessageAsync(
                    OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                    cancellationToken);
            }

            return changed;
        }
    }
}
