using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TeamPlayers.CreateTeamPlayer
{
    // Nowy profil od razu w kadrze drużyny. Handler sprawdza TeamRosterAccessGuard.
    public sealed record CreateTeamPlayerCommand(
        Guid TeamId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        PlayerPosition DefaultPosition,
        int? ShirtNumber,
        // Imię, nazwisko i data urodzenia jak u kogoś w kadrze: domyślnie błąd (pewnie dubel),
        // po potwierdzeniu w UI dodajemy mimo to.
        bool AllowDuplicate = false) : ICommand<CreateTeamPlayerResponse>, IAuthenticatedOnlyRequest;

    public sealed record CreateTeamPlayerResponse(Guid PlayerId);
}
