using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.Tournaments.CreateTournament
{
    public sealed record CreateTournamentCommand(
        Guid OrganizationId,
        string Name,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        int TeamLimit,
        string? Description) : ICommand<CreateTournamentResponse>;
}
