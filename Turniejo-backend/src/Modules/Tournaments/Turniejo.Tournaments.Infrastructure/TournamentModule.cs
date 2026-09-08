using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Infrastructure;

internal sealed class TournamentModule(ITournamentsDbContext dbContext, ISender sender) : ITournamentModule
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
            tournament.Format?.ToString(),
            tournament.OrganizationId,
            tournament.TeamLimit);
    }

    public async Task<TournamentCategoryDto?> GetTournamentCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.GetTournamentCategoryByIdAsync(categoryId, cancellationToken);
        if (category is null)
            return null;

        return new TournamentCategoryDto(
            category.Id, category.TournamentId, category.Name,
            category.MinBirthDate, category.MaxBirthDate);
    }

    public async Task<IReadOnlyList<TournamentGroupDto>> GetTournamentGroupsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var groups = await dbContext.GetTournamentGroupsByTournamentIdAsync(tournamentId, cancellationToken);

        return groups.Select(g => new TournamentGroupDto(g.Id, g.TournamentId, g.CategoryId, g.Name)).ToList();
    }

    public async Task<TournamentGroupDto?> GetTournamentGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.GetTournamentGroupByIdAsync(groupId, cancellationToken);
        if (group is null)
            return null;

        return new TournamentGroupDto(group.Id, group.TournamentId, group.CategoryId, group.Name);
    }

    public async Task<TournamentSettingsSummaryDto?> GetTournamentSettingsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.GetTournamentByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        return new TournamentSettingsSummaryDto(
            tournamentId, tournament.Settings.MatchDurationMinutes, tournament.Settings.BreakDurationMinutes,
            tournament.Settings.RefereeEditWindowMinutes, tournament.Settings.PointsForWin,
            tournament.Settings.PointsForDraw, tournament.Settings.PointsForLoss,
            tournament.Settings.IsThirdPlaceMatchEnabled, tournament.Settings.CanCoachEditSquadAfterStart,
            // Nazwy pól w encji (Home/Away) są mylące — WalkoverHomeScore to wynik zwycięzcy walkoweru,
            // nie dosłownie gospodarza; DTO cross-module nazywa to wprost, żeby moduł Matches się nie pomylił.
            tournament.Settings.WalkoverHomeScore, tournament.Settings.WalkoverAwayScore);
    }

    public async Task<IReadOnlyList<TieBreakerRuleDto>> GetTieBreakerRulesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var rules = await dbContext.GetTieBreakerRulesByTournamentIdAsync(tournamentId, cancellationToken);

        return rules
            .OrderBy(r => r.Order)
            .Select(r => new TieBreakerRuleDto(r.Order, r.Type.ToString()))
            .ToList();
    }

    public async Task<bool> ChangeTournamentStatusAsync(Guid tournamentId, string newStatus, CancellationToken cancellationToken = default)
    {
        var status = Enum.Parse<TournamentStatus>(newStatus);
        var result = await sender.Send(new ChangeTournamentStatusCommand(tournamentId, status), cancellationToken);

        return result.IsSuccess;
    }

    public async Task<PublicTournamentDto?> GetPublicTournamentBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.GetTournamentBySlugAsync(slug, cancellationToken);
        if (tournament is null || !tournament.PublicSettings.IsPublic)
            return null;

        return new PublicTournamentDto(
            tournament.Id, tournament.Name, tournament.Slug, tournament.Status.ToString(), tournament.Format?.ToString(),
            tournament.StartAt, tournament.EndAt, tournament.Description, MapPublicSettings(tournament));
    }

    public async Task<TournamentPublicSettingsDto?> GetTournamentPublicSettingsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.GetTournamentByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        return MapPublicSettings(tournament);
    }

    private static TournamentPublicSettingsDto MapPublicSettings(global::Turniejo.Tournaments.Domain.Entities.Tournament tournament) => new(
        tournament.Id, tournament.PublicSettings.IsPublic, tournament.PublicSettings.ShowTeams,
        tournament.PublicSettings.ShowPlayers, tournament.PublicSettings.ShowSchedule,
        tournament.PublicSettings.ShowStandings, tournament.PublicSettings.ShowBracket,
        tournament.PublicSettings.ShowLiveMatches);
}
