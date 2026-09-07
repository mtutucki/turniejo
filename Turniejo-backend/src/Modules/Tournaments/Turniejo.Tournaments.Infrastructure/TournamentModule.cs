using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Infrastructure;

internal sealed class TournamentModule(ITournamentsDbContext dbContext) : ITournamentModule
{
    public async Task<TournamentDto?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.GetTournamentByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        return new TournamentDto(
            tournament.Id,
            tournament.Name,
            tournament.Status.ToString(),
            tournament.OrganizationId,
            tournament.TeamLimit);
    }
}
