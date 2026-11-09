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
            var computed = await GroupTable.ComputeAsync(request.GroupId, tournamentModule, teamModule, matchModule, cancellationToken);
            if (computed.IsFailure)
                return Result.Failure<RecalculateStandingsResponse>(computed.Error);
            var group = computed.Value.Group;

            foreach (var (stat, position) in computed.Value.Rows)
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

                standing.SetPosition(position);
            }

            var domainEvent = new StandingUpdated(group.TournamentId, group.CategoryId, request.GroupId);
            var outboxMessage = OutboxMessage.Create(
                domainEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));
            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new RecalculateStandingsResponse(computed.Value.Rows.Count));
        }
    }
}
