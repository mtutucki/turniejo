namespace Turniejo.Players.Application.DTOs
{
    public sealed record PlayerGuardianDto(
        Guid Id,
        Guid PlayerId,
        Guid UserId,
        string RelationshipType,
        bool CanEdit,
        bool CanReceiveNotifications);
}
