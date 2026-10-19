using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Users.Domain.Events;

public sealed record UserRegistered(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    // Wiadomości Outboxa sprzed tej zmiany nie mają pola — deserializują się z domyślnym true.
    bool CreateOrganization = true,
    // Nazwa wpisana przy rejestracji; null = organizacja nazwana imieniem i nazwiskiem.
    string? OrganizationName = null) : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}