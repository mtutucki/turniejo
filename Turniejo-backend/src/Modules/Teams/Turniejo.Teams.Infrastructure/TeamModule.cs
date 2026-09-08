using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;

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

    public async Task<IReadOnlyList<Guid>> GetTeamStaffUserIdsAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default)
    {
        var staff = await dbContext.GetTeamStaffByTournamentTeamIdAsync(tournamentTeamId, cancellationToken);

        return staff.Select(s => s.UserId).Distinct().ToList();
    }

    public async Task<bool> AnyTeamsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyTournamentTeamsByCategoryIdAsync(categoryId, cancellationToken);

    private static TournamentTeamDto ToDto(TournamentTeam tournamentTeam) =>
        new(tournamentTeam.Id, tournamentTeam.TournamentId, tournamentTeam.TeamId,
            tournamentTeam.CategoryId, tournamentTeam.GroupId, tournamentTeam.RegistrationStatus.ToString());
}
