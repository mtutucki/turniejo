using Turniejo.Organizations.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Organizations.Infrastructure;

internal sealed class OrganizationModule(
    IOrganizationsDbContext dbContext,
    ITournamentModule tournamentModule,
    ITeamModule teamModule,
    IPlayerModule playerModule,
    IMatchModule matchModule) : IOrganizationModule
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetOrganizationNamesAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken = default)
    {
        if (organizationIds.Count == 0)
            return new Dictionary<Guid, string>();

        var organizations = await dbContext.GetOrganizationsByIdsAsync(organizationIds, cancellationToken);
        return organizations.ToDictionary(o => o.Id, o => o.Name);
    }

    // Moduły mają osobne DbContexty, więc nie ma jednej transakcji — kolejność jest dobrana tak, żeby
    // przerwane usuwanie dało się dokończyć ponownym wywołaniem: każdy krok znajduje swoje dane po
    // organizacji, a sama organizacja znika na końcu.
    public async Task<DeletedOrganizationDto> DeleteOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        // Składy przed drużynami: po usunięciu zgłoszeń nie byłoby już po czym znaleźć zawodników.
        var tournamentTeamIds = await teamModule.GetTournamentTeamIdsByOrganizationIdAsync(organizationId, cancellationToken);
        var playerIds = (await playerModule.RemovePlayersOfTournamentTeamsAsync(tournamentTeamIds, cancellationToken)).ToList();
        // Potem kadry: zawodnicy z kadr byli chronieni przed usunięciem w kroku wyżej.
        var teamIds = await teamModule.GetTeamIdsByOrganizationIdAsync(organizationId, cancellationToken);
        playerIds.AddRange(await playerModule.RemoveRostersOfTeamsAsync(teamIds, cancellationToken));
        await teamModule.RemoveTeamsByOrganizationIdAsync(organizationId, cancellationToken);

        var tournamentIds = await tournamentModule.DeleteTournamentsByOrganizationIdAsync(organizationId, cancellationToken);
        var matchIds = new List<Guid>();
        foreach (var tournamentId in tournamentIds)
            matchIds.AddRange((await matchModule.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken)).Select(m => m.Id));

        await dbContext.RemoveOrganizationAsync(organizationId, cancellationToken);

        return new DeletedOrganizationDto(tournamentIds, tournamentTeamIds, playerIds, matchIds);
    }
}
