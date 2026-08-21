using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.TournamentsCategories.UpdateTournamentCategory
{
    public sealed record UpdateTournamentCategoryCommand(
        Guid CategoryId,
        string Name,
        DateTime? MinBirthDate,
        DateTime? MaxBirthDate,
        int Order) : ICommand<UpdateTournamentCategoryResponse>;
}
