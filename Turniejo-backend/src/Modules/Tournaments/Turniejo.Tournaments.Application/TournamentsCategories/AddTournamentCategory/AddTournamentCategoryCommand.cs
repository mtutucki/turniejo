using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.TournamentsCategories.AddTournamentCategory
{
    public sealed record AddTournamentCategoryCommand(
        Guid TournamentId,
        string Name,
        DateTime? MinBirthDate,
        DateTime? MaxBirthDate,
        int Order) : ICommand<AddTournamentCategoryResponse>;
}
