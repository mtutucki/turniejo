using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.CreatePlayer
{
    public sealed record CreatePlayerCommand(
        string FirstName,
        string LastName,
        DateTime BirthDate,
        PlayerPosition DefaultPosition,
        string? PhotoUrl,
        Guid? UserId) : ICommand<CreatePlayerResponse>;
}
