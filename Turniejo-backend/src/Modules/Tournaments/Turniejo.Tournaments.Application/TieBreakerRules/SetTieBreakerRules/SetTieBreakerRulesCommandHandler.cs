using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.Application.TieBreakerRules.SetTieBreakerRules
{
    internal sealed class SetTieBreakerRulesCommandHandler(ITournamentsDbContext tournamentsDbContext, ICurrentUserContext currentUserContext) : ICommandHandler<SetTieBreakerRulesCommand, SetTieBreakerRulesResponse>
    {
        public async Task<Result<SetTieBreakerRulesResponse>> Handle(SetTieBreakerRulesCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<SetTieBreakerRulesResponse>(Error.NotFound("Tournament", request.TournamentId));

            var duplicateOrders = request.Rules
                .GroupBy(r => r.Order)
                .Any(g => g.Count() > 1);

            if (duplicateOrders)
                return Result.Failure<SetTieBreakerRulesResponse>(Error.Validation("Order", "TieBreakerRule order values must be unique."));

            var newRules = new List<TieBreakerRule>();
            foreach (var item in request.Rules)
            {
                var result = TieBreakerRule.Create(Guid.NewGuid(), request.TournamentId, item.Order, item.Type);
                if (result.IsFailure)
                    return Result.Failure<SetTieBreakerRulesResponse>(result.Error);

                newRules.Add(result.Value);
            }

            await tournamentsDbContext.ReplaceTieBreakerRulesAsync(request.TournamentId, newRules, cancellationToken);

            var outboxEvent = new TieBreakerRulesSet(
                request.TournamentId,
                tournament.OrganizationId,
                currentUserContext.UserId,
                request.Rules.Select(r => new TieBreakerRuleEntry(r.Order, r.Type)).ToList());

            var outboxMessage = OutboxMessage.Create(
                outboxEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(outboxEvent, outboxEvent.GetType()));

            await tournamentsDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            await tournamentsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new SetTieBreakerRulesResponse(newRules.Count));
        }
    }
}