using System.Text.Json;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.Brackets.ResetBracket
{
    internal sealed class ResetBracketCommandHandler(
        IBracketsDbContext dbContext,
        ITournamentModule tournamentModule,
        IMatchModule matchModule) : ICommandHandler<ResetBracketCommand, ResetBracketResponse>
    {
        public async Task<Result<ResetBracketResponse>> Handle(ResetBracketCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<ResetBracketResponse>(Error.NotFound("Tournament", request.TournamentId));

            var bracket = await dbContext.GetBracketByTournamentAndCategoryAsync(request.TournamentId, request.CategoryId, cancellationToken);
            if (bracket == null)
                return Result.Failure<ResetBracketResponse>(Error.NotFound("Bracket", request.TournamentId));

            var rounds = await dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, cancellationToken);
            var nodes = await dbContext.GetBracketNodesByRoundIdsAsync(rounds.Select(r => r.Id).ToList(), cancellationToken);
            var matchIds = nodes.Where(n => n.MatchId.HasValue).Select(n => n.MatchId!.Value).ToList();

            // Najpierw mecze, potem drabinka, na końcu etap (moduły mają osobne bazy, nie ma jednej
            // transakcji): przerwanie w połowie zostawia drabinkę bez meczów, a ponowny reset dokańcza.
            if (matchIds.Count > 0 && !await matchModule.RemoveUnstartedMatchesAsync(matchIds, cancellationToken))
                return Result.Failure<ResetBracketResponse>(Error.Conflict("Bracket", "Cannot reset the bracket after one of its matches has started"));

            await dbContext.RemoveBracketAsync(bracket, cancellationToken);

            var domainEvent = new BracketUpdated(bracket.Id, bracket.TournamentId);
            await dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Puchar: terminarz to mecze drabinki — bez nich "harmonogram gotowy" byłby pusty, a generator
            // terminarza wymaga etapu RegistrationClosed.
            if (tournament.Format == "Knockout" && tournament.Status == "ScheduleGenerated" &&
                !await tournamentModule.RevertScheduleGenerationAsync(request.TournamentId, cancellationToken))
                return Result.Failure<ResetBracketResponse>(Error.Conflict("Status", "The bracket was removed, but the tournament stage could not be reverted — clear the schedule"));

            return Result.Success(new ResetBracketResponse(matchIds.Count));
        }
    }
}
