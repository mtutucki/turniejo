namespace Turniejo.Players.Application.DTOs
{
    public sealed record MyChildDto(
        Guid PlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        string DefaultPosition,
        string? PhotoUrl,
        string RelationshipType,
        bool CanEdit);
}
