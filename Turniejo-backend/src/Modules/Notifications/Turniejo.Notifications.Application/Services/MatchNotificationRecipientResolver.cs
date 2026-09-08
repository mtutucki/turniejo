using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.Application.Services
{
    public sealed class MatchNotificationRecipientResolver(ITeamModule teamModule)
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

            return recipients.ToList();
        }
    }
}
