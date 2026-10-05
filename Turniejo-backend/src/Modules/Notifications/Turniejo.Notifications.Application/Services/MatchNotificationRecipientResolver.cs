using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.Application.Services
{
    public sealed class MatchNotificationRecipientResolver(ITeamModule teamModule, IPlayerModule playerModule)
    {
        public async Task<IReadOnlyList<Guid>> ResolveAsync(
            Guid? refereeId, Guid homeTournamentTeamId, Guid awayTournamentTeamId, CancellationToken cancellationToken = default)
        {
            var recipients = new HashSet<Guid>();

            if (refereeId.HasValue)
                recipients.Add(refereeId.Value);

            var homeStaff = await teamModule.GetTeamStaffUserIdsAsync(homeTournamentTeamId, cancellationToken);
            var awayStaff = await teamModule.GetTeamStaffUserIdsAsync(awayTournamentTeamId, cancellationToken);

            foreach (var userId in homeStaff)
                recipients.Add(userId);

            foreach (var userId in awayStaff)
                recipients.Add(userId);

            // PlayerGuardian.CanReceiveNotifications było przechowywane, ale nigdzie nieodczytywane —
            // rodzic ustawiający tę flagę nigdy nie dostawał przypomnienia/startu/wyniku meczu dziecka.
            var homeRoster = await playerModule.GetTournamentTeamPlayersAsync(homeTournamentTeamId, cancellationToken);
            var awayRoster = await playerModule.GetTournamentTeamPlayersAsync(awayTournamentTeamId, cancellationToken);
            var playerIds = homeRoster.Concat(awayRoster).Select(p => p.PlayerId).Distinct().ToList();

            if (playerIds.Count > 0)
            {
                var guardianIds = await playerModule.GetNotifiableGuardianUserIdsForPlayersAsync(playerIds, cancellationToken);
                foreach (var userId in guardianIds)
                    recipients.Add(userId);
            }

            return recipients.ToList();
        }
    }
}
