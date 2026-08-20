
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory
{
    public sealed record DeleteTournamentCategoryCommand(Guid CategoryId) : ICommand;
}
