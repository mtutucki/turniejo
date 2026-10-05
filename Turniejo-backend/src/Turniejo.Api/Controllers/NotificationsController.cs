using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Notifications.Application.Notifications.CreateOrganizerAnnouncement;
using Turniejo.Notifications.Application.Notifications.DeleteNotification;
using Turniejo.Notifications.Application.Notifications.GetMyNotifications;
using Turniejo.Notifications.Application.Notifications.MarkNotificationAsRead;

namespace Turniejo.Api.Controllers
{
    [ApiController]
    public sealed class NotificationsController(ISender sender) : ControllerBase
    {
        [HttpPost("/api/tournaments/{tournamentId}/announcements")]
        public async Task<IActionResult> CreateAnnouncement([FromRoute] Guid tournamentId, [FromBody] CreateAnnouncementRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateOrganizerAnnouncementCommand(tournamentId, request.Title, request.Message);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/notifications/{result.Value.NotificationId}", result.Value);
        }

        [HttpGet("/api/notifications")]
        public async Task<IActionResult> GetMyNotifications(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyNotificationsQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("/api/notifications/{id}/read")]
        public async Task<IActionResult> MarkAsRead([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new MarkNotificationAsReadCommand(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("/api/notifications/{id}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new DeleteNotificationCommand(id), cancellationToken);
            return result.IsFailure ? result.ToActionResult() : NoContent();
        }

        public sealed record CreateAnnouncementRequest(string Title, string Message);
    }
}
