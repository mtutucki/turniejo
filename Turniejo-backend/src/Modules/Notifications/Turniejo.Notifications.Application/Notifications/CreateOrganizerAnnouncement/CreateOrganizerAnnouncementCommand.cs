using Turniejo.Notifications.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Notifications.Application.Notifications.CreateOrganizerAnnouncement
{
    public sealed record CreateOrganizerAnnouncementCommand(
        Guid TournamentId,
        string Title,
        string Message) : ICommand<CreateOrganizerAnnouncementResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => NotificationRoles.OrganizerOnly;
    }
}
