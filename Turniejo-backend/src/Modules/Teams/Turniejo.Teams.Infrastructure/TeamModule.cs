using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Infrastructure;

internal sealed class TeamModule(ITeamsDbContext dbContext) : ITeamModule
{
    public async Task<TeamDto?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await dbContext.GetTeamByIdAsync(teamId, cancellationToken);
        if (team is null)
            return null;

        return new TeamDto(team.Id, team.Name, team.ShortName, team.LogoUrl);
    }

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0)
            return [];

        var teams = await dbContext.GetTeamsByIdsAsync(teamIds, cancellationToken);

        return teams.Select(t => new TeamDto(t.Id, t.Name, t.ShortName, t.LogoUrl)).ToList();
    }

    public async Task<Guid?> GetTeamOrganizationIdAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        (await dbContext.GetTeamByIdAsync(teamId, cancellationToken))?.OrganizationId;

    public async Task<IReadOnlyList<Guid>> GetTeamIdsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        (await dbContext.GetTeamsByOrganizationIdAsync(organizationId, cancellationToken)).Select(t => t.Id).ToList();

    public async Task<TournamentTeamDto?> GetTournamentTeamAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default)
    {
        var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, cancellationToken);
        if (tournamentTeam is null)
            return null;

        return ToDto(tournamentTeam);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountAcceptedTeamsByTournamentIdsAsync(IReadOnlyCollection<Guid> tournamentIds, CancellationToken cancellationToken = default)
    {
        if (tournamentIds.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await dbContext.CountAcceptedTeamsByTournamentIdsAsync(tournamentIds, cancellationToken);
        return tournamentIds.Distinct().ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    public async Task<IReadOnlyList<TournamentTeamDto>> GetTournamentTeamsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournamentTeams = await dbContext.GetTournamentTeamsByTournamentIdAsync(tournamentId, cancellationToken);

        return tournamentTeams.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TournamentTeamDto>> GetTournamentTeamsByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var tournamentTeams = await dbContext.GetTournamentTeamsByTeamIdAsync(teamId, cancellationToken);

        return tournamentTeams.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetTeamStaffUserIdsAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default)
    {
        var staff = await dbContext.GetTeamStaffByTournamentTeamIdAsync(tournamentTeamId, cancellationToken);

        return staff.Select(s => s.UserId).Distinct().ToList();
    }

    public async Task<bool> AnyActiveTeamsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyActiveTournamentTeamsByCategoryIdAsync(categoryId, cancellationToken);

    public Task<IReadOnlyList<Guid>> GetInactiveTournamentTeamIdsByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        dbContext.GetInactiveTournamentTeamIdsByCategoryIdAsync(categoryId, cancellationToken);

    public Task RemoveTournamentTeamsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default) =>
        dbContext.RemoveTournamentTeamsAsync(tournamentTeamIds, cancellationToken);

    public async Task<bool> AnyActiveTeamsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyActiveTournamentTeamsByGroupIdAsync(groupId, cancellationToken);

    public async Task UnassignTeamsFromGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var teams = await dbContext.GetTournamentTeamsByGroupIdForUpdateAsync(groupId, cancellationToken);
        foreach (var team in teams)
            team.AssignToGroup(null);

        if (teams.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> AssignUngroupedTeamsToGroupAsync(Guid tournamentId, Guid categoryId, Guid groupId, CancellationToken cancellationToken = default)
    {
        // Zapytanie śledzone — AssignToGroup ma trafić do bazy przy SaveChanges.
        var teams = (await dbContext.GetUngroupedTournamentTeamsForUpdateAsync(tournamentId, categoryId, cancellationToken))
            .Where(t => t.RegistrationStatus is not (RegistrationStatus.Rejected or RegistrationStatus.Withdrawn))
            .ToList();
        foreach (var team in teams)
            team.AssignToGroup(groupId);

        if (teams.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return teams.Count;
    }

    public Task<IReadOnlyList<Guid>> GetTournamentTeamIdsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        dbContext.GetTournamentTeamIdsByOrganizationIdAsync(organizationId, cancellationToken);

    public Task RemoveTeamsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        dbContext.RemoveTeamsByOrganizationIdAsync(organizationId, cancellationToken);

    private static TournamentTeamDto ToDto(TournamentTeam tournamentTeam) =>
        new(tournamentTeam.Id, tournamentTeam.TournamentId, tournamentTeam.TeamId,
            tournamentTeam.CategoryId, tournamentTeam.GroupId, tournamentTeam.RegistrationStatus.ToString());
}
