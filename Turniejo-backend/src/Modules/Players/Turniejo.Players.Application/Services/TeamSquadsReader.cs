using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.Application.Services
{
    // Składy turniejowe zawodników jednej drużyny, podzielone na bieżące (turniej trwa albo jest przed nami)
    // i zakończone. Wspólne dla listy kadry i usuwania z kadry, żeby pytanie i wykonanie widziały to samo.
    public sealed class TeamSquadsReader(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule,
        SquadEditGuard squadEditGuard,
        IAuthorizationService authorizationService)
    {
        public sealed record SquadEntry(TournamentPlayer TournamentPlayer, TeamRosterSquadDto Squad, bool IsCurrent);

        public async Task<IReadOnlyList<SquadEntry>> ReadAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
        {
            var registrations = (await teamModule.GetTournamentTeamsByTeamIdAsync(teamId, cancellationToken))
                .Where(r => r.RegistrationStatus is not ("Rejected" or "Withdrawn"))
                .ToList();

            var entries = new List<SquadEntry>();
            var canEditByTournament = new Dictionary<Guid, bool>();
            foreach (var registration in registrations)
            {
                var squad = (await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(registration.Id, cancellationToken))
                    .Where(tp => tp.Status != TournamentPlayerStatus.Removed)
                    .ToList();
                if (squad.Count == 0)
                    continue;

                var tournament = await tournamentModule.GetTournamentAsync(registration.TournamentId, cancellationToken);
                if (tournament == null)
                    continue;

                var isCurrent = tournament.Status is not ("Finished" or "Cancelled");
                var canEdit = isCurrent && await CanEditSquadAsync(registration.Id, tournament.Id, userId, canEditByTournament, cancellationToken);

                var dto = new TeamRosterSquadDto(registration.Id, tournament.Id, tournament.Name, tournament.Status, tournament.StartAt, canEdit);
                entries.AddRange(squad.Select(tp => new SquadEntry(tp, dto, isCurrent)));
            }

            return entries;
        }

        // Jak przy zwykłej edycji składu: organizator turnieju albo trener tego zgłoszenia, a do tego
        // blokada składu po starcie turnieju (SquadEditGuard).
        private async Task<bool> CanEditSquadAsync(
            Guid tournamentTeamId, Guid tournamentId, Guid userId, Dictionary<Guid, bool> guardByTournament, CancellationToken cancellationToken)
        {
            var hasRole =
                await authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, PlayerRoles.OrganizerOnly, cancellationToken) ||
                await authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.TournamentTeam), tournamentTeamId, PlayerRoles.CoachOnly, cancellationToken);
            if (!hasRole)
                return false;

            if (guardByTournament.TryGetValue(tournamentId, out var allowed))
                return allowed;

            try
            {
                await squadEditGuard.EnsureCanEditSquadAsync(tournamentId, userId, cancellationToken);
                allowed = true;
            }
            catch (UnauthorizedException)
            {
                allowed = false;
            }

            guardByTournament[tournamentId] = allowed;
            return allowed;
        }
    }
}
