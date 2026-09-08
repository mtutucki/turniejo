using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.Tournaments.JoinTournament
{
    // Sam kod dostępu JEST autoryzacją — każdy zalogowany user z poprawnym kodem może dołączyć.
    public sealed record JoinTournamentCommand(string Code) : ICommand<JoinTournamentResponse>, IAuthenticatedOnlyRequest;
}
