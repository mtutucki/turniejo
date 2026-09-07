using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.UpdatePlayer
{
    public sealed record UpdatePlayerCommand(
        Guid PlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        PlayerPosition DefaultPosition,
        string? PhotoUrl) : ICommand<UpdatePlayerResponse>;
}
