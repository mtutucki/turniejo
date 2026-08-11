using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournament
{
    public sealed record UpdateTournamentDetailsCommand(
        Guid TournamentId,
        string Name,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        int TeamLimit,
        string? Description
    ) : ICommand<UpdateTournamentDetailsResponse>;
}
