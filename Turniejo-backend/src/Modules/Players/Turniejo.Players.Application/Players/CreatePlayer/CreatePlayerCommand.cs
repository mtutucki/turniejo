using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.CreatePlayer
{
    // Świadomie bez dalszych ograniczeń — utworzenie nowego profilu zawodnika (jeszcze nieprzypisanego
    // do żadnej drużyny) jest bezpieczne dla każdego zalogowanego usera; realne uprawnienia zaczynają
    // się przy AssignPlayerToTournamentTeamCommand (Organizer/Coach) i PlayerAccessGuard (edycja/odczyt).
    public sealed record CreatePlayerCommand(
        string FirstName,
        string LastName,
        DateTime BirthDate,
        PlayerPosition DefaultPosition,
        string? PhotoUrl,
        Guid? UserId) : ICommand<CreatePlayerResponse>, IAuthenticatedOnlyRequest;
}
