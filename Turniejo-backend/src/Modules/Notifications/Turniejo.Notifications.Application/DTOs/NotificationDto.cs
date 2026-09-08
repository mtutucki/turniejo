namespace Turniejo.Notifications.Application.DTOs
{
    public sealed record NotificationDto(
        Guid NotificationRecipientId,
        Guid NotificationId,
        Guid? TournamentId,
        Guid? MatchId,
        string Type,
        string Title,
        string Message,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReadAt);
}
