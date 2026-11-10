using System.Text.Json;
using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus;
using Turniejo.Tournaments.Application.Tournaments.RevertScheduleGeneration;
using Turniejo.Tournaments.Domain.Entities;
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
            tournament.TeamLimit,
            tournament.StartAt,
            tournament.EndAt);
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

        return groups.Select(g => new TournamentGroupDto(g.Id, g.TournamentId, g.CategoryId, g.Name, g.Stage)).ToList();
    }

    public async Task<TournamentGroupDto?> GetTournamentGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.GetTournamentGroupByIdAsync(groupId, cancellationToken);
        if (group is null)
            return null;

        return new TournamentGroupDto(group.Id, group.TournamentId, group.CategoryId, group.Name, group.Stage);
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

        // Ta sama domyślna kolejność co w GET /tiebreaker-rules, żeby tabela liczyła to, co widzi organizator.
        if (rules.Count == 0)
            return TieBreakerRule.DefaultOrder.Select((type, i) => new TieBreakerRuleDto(i + 1, type.ToString())).ToList();

        return rules
            .OrderBy(r => r.Order)
            .Select(r => new TieBreakerRuleDto(r.Order, r.Type.ToString()))
            .ToList();
    }

    public async Task<bool> ChangeTournamentStatusAsync(Guid tournamentId, string newStatus, CancellationToken cancellationToken = default)
    {
        var status = Enum.Parse<TournamentStatus>(newStatus);
        // IsScheduleTransition: ScheduleGenerated wolno ustawić tylko generatorowi terminarza, nie ręcznie przez API.
        var result = await sender.Send(new ChangeTournamentStatusCommand(tournamentId, status, IsScheduleTransition: true), cancellationToken);

        return result.IsSuccess;
    }

    public async Task<bool> MarkTournamentStartedAsync(Guid tournamentId, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.GetTournamentByIdAsync(tournamentId, cancellationToken);
        if (tournament is null || tournament.Status != TournamentStatus.ScheduleGenerated)
            return false;

        if (tournament.ChangeStatus(TournamentStatus.InProgress, changedByUserId).IsFailure)
            return false;

        foreach (var domainEvent in tournament.DomainEvents)
        {
            await dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        tournament.ClearDomainEvents();
        return true;
    }

    public async Task<bool> RevertScheduleGenerationAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new RevertScheduleGenerationCommand(tournamentId), cancellationToken);
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

    public Task<IReadOnlyList<Guid>> DeleteTournamentsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        dbContext.SoftDeleteTournamentsByOrganizationIdAsync(organizationId, cancellationToken);

    private static TournamentPublicSettingsDto MapPublicSettings(global::Turniejo.Tournaments.Domain.Entities.Tournament tournament) => new(
        tournament.Id, tournament.PublicSettings.IsPublic, tournament.PublicSettings.ShowTeams,
        tournament.PublicSettings.ShowPlayers, tournament.PublicSettings.ShowSchedule,
        tournament.PublicSettings.ShowStandings, tournament.PublicSettings.ShowBracket,
        tournament.PublicSettings.ShowLiveMatches, tournament.PublicSettings.ShowStats,
        tournament.PublicSettings.AbbreviatePlayerNames, tournament.PublicSettings.ShowInDirectory);
}
