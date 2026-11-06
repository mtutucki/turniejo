using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TeamPlayers.RemoveTeamPlayer
{
    // Usunięcie z kadry drużyny. Profil i historia zostają (zawodnik dostaje status Left), chyba że zawodnik
    // nigdzie nie grał i nie ma konta ani opiekunów: wtedy znika całkiem, jak dodany pomyłkowo.
    // RemoveFromCurrentSquads decyduje usuwający: czy zdjąć go też ze składów turniejów, które trwają albo
    // się nie zaczęły. Zakończone turnieje zostają nietknięte. Handler sprawdza TeamRosterAccessGuard.
    public sealed record RemoveTeamPlayerCommand(
        Guid TeamId,
        Guid PlayerId,
        bool RemoveFromCurrentSquads) : ICommand<RemoveTeamPlayerResponse>, IAuthenticatedOnlyRequest;

    public sealed record RemoveTeamPlayerResponse(
        Guid PlayerId,
        // true: profil usunięty całkiem; false: zawodnik został jako były zawodnik drużyny.
        bool Deleted,
        int RemovedFromSquads,
        // Turnieje, w których zawodnik został w składzie, bo usuwający nie może go zmienić.
        IReadOnlyList<string> KeptInSquads);
}
