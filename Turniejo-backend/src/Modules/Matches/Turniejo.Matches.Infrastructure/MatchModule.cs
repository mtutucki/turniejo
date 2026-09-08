using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.CreateBracketMatch;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Infrastructure;

internal sealed class MatchModule(IMatchesDbContext dbContext, ISender sender) : IMatchModule
{
    public async Task<IReadOnlyList<GroupMatchDto>> GetFinishedMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.GetFinishedMatchesByGroupIdAsync(groupId, cancellationToken);

        return matches
            .Select(m => new GroupMatchDto(m.Id, groupId, m.HomeTournamentTeamId, m.AwayTournamentTeamId, m.HomeScore, m.AwayScore))
            .ToList();
    }

    public async Task<Guid> CreateBracketMatchAsync(
        Guid tournamentId, Guid? categoryId, Guid homeTournamentTeamId, Guid awayTournamentTeamId,
        string stageType, CancellationToken cancellationToken = default)
    {
        var command = new CreateBracketMatchCommand(
            tournamentId, categoryId, homeTournamentTeamId, awayTournamentTeamId, Enum.Parse<StageType>(stageType));

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

    private static TournamentMatchDto ToTournamentMatchDto(Turniejo.Matches.Domain.Entities.Match m) =>
        new(m.Id, m.TournamentId, m.Status.ToString(), m.ScheduledAt, m.HomeTournamentTeamId, m.AwayTournamentTeamId, m.HomeScore, m.AwayScore);
}
