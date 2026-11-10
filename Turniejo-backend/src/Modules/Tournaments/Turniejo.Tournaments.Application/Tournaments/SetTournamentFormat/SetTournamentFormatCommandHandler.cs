using System.Text.Json;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Services;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat
{
    internal sealed class SetTournamentFormatCommandHandler(
        ITournamentsDbContext tournamentsDbContext,
        DefaultGroupProvisioner defaultGroupProvisioner) : ICommandHandler<SetTournamentFormatCommand, SetTournamentFormatResponse>
    {
        public async Task<Result<SetTournamentFormatResponse>> Handle(SetTournamentFormatCommand request, CancellationToken cancellationToken)
        {
            var result = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (result == null)
                return Result.Failure<SetTournamentFormatResponse>(Error.NotFound("Tournament", request.TournamentId));

            // Grupy drugiej fazy mają sens tylko w formacie dwóch faz; w innym zostałyby puste i bez meczów.
            if (result.Format == TournamentFormat.TwoGroupStages && request.TournamentFormat != TournamentFormat.TwoGroupStages &&
                (await tournamentsDbContext.GetTournamentGroupsByTournamentIdAsync(result.Id, cancellationToken)).Any(g => g.Stage == 2))
                return Result.Failure<SetTournamentFormatResponse>(Error.Validation("Format", "Remove the second-stage groups before changing the format"));

            var setFormatResult = result.SetFormat(request.TournamentFormat);

            if (setFormatResult.IsFailure)
                return Result.Failure<SetTournamentFormatResponse>(setFormatResult.Error);

            foreach (var domainEvent in result.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await tournamentsDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            result.ClearDomainEvents();

            // Format ustawiony po utworzeniu kategorii — kategorie bez grup dostają "Grupa A" teraz.
            var categories = await tournamentsDbContext.GetTournamentCategoriesByTournamentIdAsync(result.Id, cancellationToken);
            await defaultGroupProvisioner.EnsureDefaultGroupsAsync(result, categories.Select(c => c.Id).ToList(), cancellationToken);

            return Result.Success(new SetTournamentFormatResponse(result.Id, result.Slug));
        }
    }
}
