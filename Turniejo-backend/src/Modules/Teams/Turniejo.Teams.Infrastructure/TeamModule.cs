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

        return new TeamDto(team.Id, team.Name, team.ShortName);
    }

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0)
            return [];

        var teams = await dbContext.GetTeamsByIdsAsync(teamIds, cancellationToken);

        return teams.Select(t => new TeamDto(t.Id, t.Name, t.ShortName)).ToList();
    }

    public async Task<TournamentTeamDto?> GetTournamentTeamAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default)
    {
        var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, cancellationToken);
        if (tournamentTeam is null)
            return null;

        return ToDto(tournamentTeam);
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

    public async Task<bool> AnyTeamsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyTournamentTeamsByCategoryIdAsync(categoryId, cancellationToken);

    public async Task<bool> AnyTeamsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyTournamentTeamsByGroupIdAsync(groupId, cancellationToken);

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
