using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.CreateBracketMatch;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Infrastructure;

internal sealed class MatchModule(IMatchesDbContext dbContext, ISender sender, IUserModule userModule) : IMatchModule
{
    public async Task<IReadOnlyDictionary<Guid, int>> CountLiveMatchesByTournamentIdsAsync(IReadOnlyCollection<Guid> tournamentIds, CancellationToken cancellationToken = default)
    {
        if (tournamentIds.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await dbContext.CountLiveMatchesByTournamentIdsAsync(tournamentIds, cancellationToken);
        return tournamentIds.Distinct().ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    public async Task<IReadOnlyList<PitchInfoDto>> GetPitchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var pitches = await dbContext.GetPitchesByTournamentIdAsync(tournamentId, cancellationToken);
        return pitches.Select(p => new PitchInfoDto(p.Id, p.Name, p.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<GroupMatchDto>> GetFinishedMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.GetFinishedMatchesByGroupIdAsync(groupId, cancellationToken);

        return matches
            .Select(m => new GroupMatchDto(m.Id, groupId, m.HomeTournamentTeamId, m.AwayTournamentTeamId, m.HomeScore, m.AwayScore))
            .ToList();
    }

    public async Task<Guid> CreateSecondStageMatchAsync(
        Guid tournamentId, Guid categoryId, Guid groupId, Guid homeTournamentTeamId, Guid awayTournamentTeamId,
        Guid? plannedPitchId, DateTimeOffset? plannedAt, Guid? plannedRefereeId, CancellationToken cancellationToken = default)
    {
        var command = new CreateBracketMatchCommand(
            tournamentId, categoryId, homeTournamentTeamId, awayTournamentTeamId, StageType.Group,
            plannedPitchId, plannedAt, plannedRefereeId, groupId);

        var result = await sender.Send(command, cancellationToken);

        return result.Value.MatchId;
    }

    public async Task<Guid> CreateBracketMatchAsync(
        Guid tournamentId, Guid? categoryId, Guid homeTournamentTeamId, Guid awayTournamentTeamId,
        string stageType, Guid? plannedPitchId, DateTimeOffset? plannedAt, Guid? plannedRefereeId = null,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateBracketMatchCommand(
            tournamentId, categoryId, homeTournamentTeamId, awayTournamentTeamId, Enum.Parse<StageType>(stageType),
            plannedPitchId, plannedAt, plannedRefereeId);

        var result = await sender.Send(command, cancellationToken);

        return result.Value.MatchId;
    }

    public async Task<IReadOnlyList<TournamentMatchDto>> GetMatchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken);

        return matches.Select(ToTournamentMatchDto).ToList();
    }

    public async Task<MatchInfoDto?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.GetMatchByIdAsync(matchId, cancellationToken);
        if (match == null)
            return null;

        return new MatchInfoDto(
            match.Id, match.TournamentId, match.HomeTournamentTeamId, match.AwayTournamentTeamId,
            match.RefereeId, match.PitchId, match.ScheduledAt, match.Status.ToString());
    }

    public async Task<IReadOnlyList<TournamentMatchDto>> GetMatchesByTournamentTeamIdsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.GetMatchesByTournamentTeamIdsAsync(tournamentTeamIds, cancellationToken);

        return matches.Select(ToTournamentMatchDto).ToList();
    }

    public async Task<IReadOnlyList<TeamMatchEventDto>> GetMatchEventsByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default)
    {
        var events = await dbContext.GetMatchEventsByTournamentTeamIdAsync(tournamentTeamId, cancellationToken);

        return events
            .Select(e => new TeamMatchEventDto(e.MatchId, e.Type.ToString(), e.PlayerId, e.RelatedPlayerId))
            .ToList();
    }

    public async Task<IReadOnlyList<MatchInfoDto>> GetMatchesScheduledBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.GetMatchesScheduledBetweenAsync(from, to, cancellationToken);

        return matches
            .Select(m => new MatchInfoDto(
                m.Id, m.TournamentId, m.HomeTournamentTeamId, m.AwayTournamentTeamId,
                m.RefereeId, m.PitchId, m.ScheduledAt, m.Status.ToString()))
            .ToList();
    }

    public async Task<bool> AnyMatchesForCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyMatchesByCategoryIdAsync(categoryId, cancellationToken);

    public async Task<bool> AnyMatchesForGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyMatchesByGroupIdAsync(groupId, cancellationToken);

    public async Task<bool> IsGroupStageFinishedAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await dbContext.AnyMatchesByGroupIdAsync(groupId, cancellationToken) &&
        !await dbContext.AnyUnfinishedMatchesByGroupIdAsync(groupId, cancellationToken);

    public async Task<bool> RemoveUnstartedMatchesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.GetTrackedMatchesByIdsAsync(matchIds, cancellationToken);
        if (matches.Any(m => m.Status is MatchStatus.Live or MatchStatus.Paused or MatchStatus.Finished))
            return false;

        // Sędzia zachowałby rolę Referee w usuniętym meczu (UserRoleAssignment ze scope Match).
        foreach (var match in matches.Where(m => m.RefereeId.HasValue))
        {
            await userModule.RevokeRoleAsync(
                match.RefereeId!.Value, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, cancellationToken);
        }

        await dbContext.RemoveMatchesAsync(matches, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ChangeUnstartedMatchTeamsAsync(Guid matchId, Guid homeTournamentTeamId, Guid awayTournamentTeamId, CancellationToken cancellationToken = default)
    {
        var match = (await dbContext.GetTrackedMatchesByIdsAsync([matchId], cancellationToken)).FirstOrDefault();
        if (match == null || match.ChangeTeams(homeTournamentTeamId, awayTournamentTeamId).IsFailure)
            return false;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> RefreshKnockoutWinnersAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default)
    {
        var finished = (await dbContext.GetTrackedMatchesByIdsAsync(matchIds, cancellationToken))
            .Where(m => m.Status == MatchStatus.Finished)
            .ToList();

        var changed = false;
        foreach (var match in finished)
            changed |= match.RefreshKnockoutWinner();

        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);

        return finished.ToDictionary(m => m.Id, m => m.WinnerTournamentTeamId);
    }

    private static TournamentMatchDto ToTournamentMatchDto(Turniejo.Matches.Domain.Entities.Match m) =>
        new(m.Id, m.TournamentId, m.Status.ToString(), m.ScheduledAt, m.HomeTournamentTeamId, m.AwayTournamentTeamId, m.HomeScore, m.AwayScore,
            m.StageType.ToString(), m.GroupId, m.CategoryId, m.HomePenaltyScore, m.AwayPenaltyScore);
}
