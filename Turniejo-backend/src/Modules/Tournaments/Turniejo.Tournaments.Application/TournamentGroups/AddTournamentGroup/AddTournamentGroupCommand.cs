using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.TournamentGroups.AddTournamentGroup
{
    public sealed record AddTournamentGroupCommand(
        Guid TournamentId,
        Guid CategoryId,
        string Name,
        int Order) : ICommand<AddTournamentGroupResponse>;
}
