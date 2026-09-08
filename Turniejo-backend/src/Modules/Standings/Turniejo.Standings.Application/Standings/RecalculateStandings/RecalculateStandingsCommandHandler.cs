using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Domain.Entities;
using Turniejo.Standings.Domain.Events;

namespace Turniejo.Standings.Application.Standings.RecalculateStandings
{
    internal sealed class RecalculateStandingsCommandHandler(
        IStandingsDbContext dbContext,
        ITournamentModule tournamentModule,
        ITeamModule teamModule,
        IMatchModule matchModule) : ICommandHandler<RecalculateStandingsCommand, RecalculateStandingsResponse>
    {
        public async Task<Result<RecalculateStandingsResponse>> Handle(RecalculateStandingsCommand request, CancellationToken cancellationToken)
        {
            var group = await tournamentModule.GetTournamentGroupAsync(request.GroupId, cancellationToken);
            if (group == null)
                return Result.Failure<RecalculateStandingsResponse>(Error.NotFound("TournamentGroup", request.GroupId));

            var settings = await tournamentModule.GetTournamentSettingsAsync(group.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<RecalculateStandingsResponse>(Error.NotFound("TournamentSettings", group.TournamentId));

            var tieBreakerRules = await tournamentModule.GetTieBreakerRulesAsync(group.TournamentId, cancellationToken);

            var allTeams = await teamModule.GetTournamentTeamsByTournamentIdAsync(group.TournamentId, cancellationToken);
            var groupTeams = allTeams
                .Where(t => t.GroupId == request.GroupId && t.RegistrationStatus == "Accepted")
                .ToList();

            var matches = await matchModule.GetFinishedMatchesByGroupIdAsync(request.GroupId, cancellationToken);

            var stats = groupTeams
                .Select(t => StandingCalculator.ComputeStats(t.Id, matches, settings.PointsForWin, settings.PointsForDraw, settings.PointsForLoss))
                .ToList();

            var sortedTeamIds = StandingCalculator.SortTeams(
                stats, matches, tieBreakerRules, settings.PointsForWin, settings.PointsForDraw, settings.PointsForLoss).ToList();

            foreach (var stat in stats)
            {
                var standing = await dbContext.GetStandingAsync(request.GroupId, stat.TournamentTeamId, cancellationToken);
                if (standing == null)
                {
                    var createResult = Standing.Create(Guid.NewGuid(), group.TournamentId, group.CategoryId, request.GroupId, stat.TournamentTeamId);
                    if (createResult.IsFailure)
                        return Result.Failure<RecalculateStandingsResponse>(createResult.Error);

                    standing = createResult.Value;
                    await dbContext.AddStandingAsync(standing, cancellationToken);
                }

                var updateResult = standing.UpdateStats(stat.MatchesPlayed, stat.Wins, stat.Draws, stat.Losses, stat.GoalsFor, stat.GoalsAgainst, stat.Points);
                if (updateResult.IsFailure)
                    return Result.Failure<RecalculateStandingsResponse>(updateResult.Error);

                var position = sortedTeamIds.IndexOf(stat.TournamentTeamId) + 1;
                standing.SetPosition(position);
            }

            var domainEvent = new StandingUpdated(group.TournamentId, group.CategoryId, request.GroupId);
            var outboxMessage = OutboxMessage.Create(
                domainEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));
            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new RecalculateStandingsResponse(stats.Count));
        }
    }
}
