using System.Text.Json;
using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.SetMatchScore
{
    // Ręczna zmiana wyniku w trakcie meczu. Wynik zawsze liczy się ze zdarzeń, więc zamiast nadpisywać
    // liczby dopisujemy brakujące gole bez strzelca (albo cofamy nadmiarowe) - inaczej ręczny wynik
    // przepadał przy następnym golu (QA v3: MNZ-018). Strzelców da się potem uzupełnić.
    internal sealed class SetMatchScoreCommandHandler(
        IMatchesDbContext dbContext,
        GoalAdjuster goalAdjuster,
        ISender sender,
        ICurrentUserContext currentUserContext) : ICommandHandler<SetMatchScoreCommand, SetMatchScoreResponse>
    {
        public async Task<Result<SetMatchScoreResponse>> Handle(SetMatchScoreCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<SetMatchScoreResponse>(Error.NotFound("Match", request.MatchId));

            if (match.Status == MatchStatus.Finished)
                return Result.Failure<SetMatchScoreResponse>(Error.Conflict("Status", "Use CorrectFinishedMatchScoreCommand to change the score of a finished match"));
            if (match.Status is not (MatchStatus.Live or MatchStatus.Paused))
                return Result.Failure<SetMatchScoreResponse>(Error.Conflict("Status", "Match must be live or paused to set a manual score"));
            if (request.HomeScore < 0 || request.AwayScore < 0)
                return Result.Failure<SetMatchScoreResponse>(Error.Validation("Score", "Score cannot be negative"));

            var oldValue = JsonSerializer.Serialize(new { match.HomeScore, match.AwayScore });

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);
            var minute = match.GetElapsedSeconds(DateTimeOffset.UtcNow) / 60;
            await goalAdjuster.AdjustToAsync(
                match, events, request.HomeScore, request.AwayScore, minute, currentUserContext.UserId, cancellationToken);

            var auditLogResult = AuditLog.Create(
                Guid.NewGuid(), currentUserContext.UserId, "Match", match.Id, "SetManualScore", oldValue,
                JsonSerializer.Serialize(new { HomeScore = request.HomeScore, AwayScore = request.AwayScore }));
            if (auditLogResult.IsFailure)
                return Result.Failure<SetMatchScoreResponse>(auditLogResult.Error);

            await dbContext.AddAuditLogAsync(auditLogResult.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var recalc = await sender.Send(new RecalculateMatchScoreCommand(match.Id), cancellationToken);
            if (recalc.IsFailure)
                return Result.Failure<SetMatchScoreResponse>(recalc.Error);

            return Result.Success(new SetMatchScoreResponse(match.Id, request.HomeScore, request.AwayScore));
        }
    }
}
