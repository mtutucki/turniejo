using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.TournamentGroups.AddTournamentGroup
{
    internal sealed class AddTournamentGroupCommandHandler(ITournamentsDbContext tournamentsDbContext, ICurrentUserContext currentUserContext) : ICommandHandler<AddTournamentGroupCommand, AddTournamentGroupResponse>
    {
        public async Task<Result<AddTournamentGroupResponse>> Handle(AddTournamentGroupCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<AddTournamentGroupResponse>(Error.NotFound("Tournament", request.TournamentId));

            var tournamentCategory = await tournamentsDbContext.GetTournamentCategoryByIdAsync(request.CategoryId, cancellationToken);
            if (tournamentCategory == null)
                return Result.Failure<AddTournamentGroupResponse>(Error.NotFound("TournamentCategory", request.CategoryId));

            if (tournamentCategory.TournamentId != request.TournamentId)
                return Result.Failure<AddTournamentGroupResponse>(Error.Validation("TournamentId", "TournamentId does not match the tournament assigned to this category."));

            if (request.Stage == 2 && tournament.Format != TournamentFormat.TwoGroupStages)
                return Result.Failure<AddTournamentGroupResponse>(Error.Validation("Stage", "Second-stage groups need the two group stages format"));

            if (await tournamentsDbContext.GroupNameExistsAsync(tournamentCategory.Id, request.Name, cancellationToken))
                return Result.Failure<AddTournamentGroupResponse>(Error.Validation("Name", "Name already exists"));

            var result = TournamentGroup.Create(
                Guid.NewGuid(), tournament.OrganizationId, tournament.Id, currentUserContext.UserId, tournamentCategory.Id, request.Name, request.Order, request.Stage);

            if (result.IsFailure)
                return Result.Failure<AddTournamentGroupResponse>(result.Error);

            await tournamentsDbContext.AddTournamentGroupAsync(result.Value, cancellationToken);

            foreach (var domainEvent in result.Value.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await tournamentsDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddTournamentGroupResponse(result.Value.Id));
        }
    }
}
