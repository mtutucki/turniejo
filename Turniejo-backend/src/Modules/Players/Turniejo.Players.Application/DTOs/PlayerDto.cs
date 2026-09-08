namespace Turniejo.Players.Application.DTOs
{
    public sealed record PlayerDto(
        Guid Id,
        Guid? UserId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        string DefaultPosition,
        string? PhotoUrl);
}
