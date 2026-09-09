using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Notifications.Application.Services
{
    // Odbiorcy komunikatu "cały turniej" (ogłoszenie organizatora, anulowanie turnieju) to NIE tylko
    // role Tournament-scoped (organizator + ci, co dołączyli kodem) — trener/manager ma rolę scoped na
    // TournamentTeam, sędzia na Match, a rodzic to w ogóle nie UserRoleAssignment tylko PlayerGuardian.
    // Samo GetUserIdsByScopeAsync(Tournament, ...) cicho pomijało te trzy grupy.
    public sealed class TournamentParticipantResolver(
        IUserModule userModule, ITeamModule teamModule, IMatchModule matchModule, IPlayerModule playerModule)
    {
        public async Task<IReadOnlyList<Guid>> ResolveAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        {
            var recipients = new HashSet<Guid>();

            var tournamentScoped = await userModule.GetUserIdsByScopeAsync(
                nameof(RoleScopeType.Tournament), tournamentId, cancellationToken);
            foreach (var userId in tournamentScoped)
                recipients.Add(userId);

            var teams = await teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, cancellationToken);
            var playerIds = new List<Guid>();

            foreach (var team in teams)
            {
                var staffIds = await teamModule.GetTeamStaffUserIdsAsync(team.Id, cancellationToken);
                foreach (var userId in staffIds)
                    recipients.Add(userId);

                var roster = await playerModule.GetTournamentTeamPlayersAsync(team.Id, cancellationToken);
                playerIds.AddRange(roster.Select(p => p.PlayerId));
            }

            if (playerIds.Count > 0)
            {
                var guardianIds = await playerModule.GetNotifiableGuardianUserIdsForPlayersAsync(playerIds, cancellationToken);
                foreach (var userId in guardianIds)
                    recipients.Add(userId);
            }

            var matches = await matchModule.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken);
            foreach (var match in matches)
            {
                var refereeScoped = await userModule.GetUserIdsByScopeAsync(
                    nameof(RoleScopeType.Match), match.Id, cancellationToken);
                foreach (var userId in refereeScoped)
                    recipients.Add(userId);
            }

            return recipients.ToList();
        }
    }
}
