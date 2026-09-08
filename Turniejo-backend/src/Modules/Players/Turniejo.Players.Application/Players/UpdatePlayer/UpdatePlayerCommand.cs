using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.UpdatePlayer
{
    // Handler weryfikuje PlayerAccessGuard.CanEditAsync (self/guardian z CanEdit/Organizer/Coach).
    public sealed record UpdatePlayerCommand(
        Guid PlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        PlayerPosition DefaultPosition,
        string? PhotoUrl) : ICommand<UpdatePlayerResponse>, IAuthenticatedOnlyRequest;
}
