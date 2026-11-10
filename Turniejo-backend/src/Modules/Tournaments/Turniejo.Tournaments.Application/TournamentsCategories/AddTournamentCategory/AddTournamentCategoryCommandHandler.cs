using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Services;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Application.TournamentsCategories.AddTournamentCategory
{
    internal sealed class AddTournamentCategoryCommandHandler(
        ITournamentsDbContext tournamentsDbContext,
        ICurrentUserContext currentUserContext,
        DefaultGroupProvisioner defaultGroupProvisioner) : ICommandHandler<AddTournamentCategoryCommand, AddTournamentCategoryResponse>
    {
        public async Task<Result<AddTournamentCategoryResponse>> Handle(AddTournamentCategoryCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<AddTournamentCategoryResponse>(Error.NotFound("Tournament", request.TournamentId));

            var tournamentCategoryNameExists = await tournamentsDbContext.CategoryNameExistsAsync(request.TournamentId, request.Name, cancellationToken);
            if (tournamentCategoryNameExists)
                return Result.Failure<AddTournamentCategoryResponse>(Error.Validation("Name", $"Tournament category with name '{request.Name}' already exists"));

            var result = TournamentCategory.Create(
                Guid.NewGuid(),
                tournament.OrganizationId,
                request.TournamentId,
                currentUserContext.UserId,
                request.Name,
                request.MinBirthDate,
                request.MaxBirthDate,
                request.Order);

            if (result.IsFailure)
                return Result.Failure<AddTournamentCategoryResponse>(result.Error);

            await tournamentsDbContext.AddTournamentCategoryAsync(result.Value, cancellationToken);

            foreach (var domainEvent in result.Value.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await tournamentsDbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);

            // Format z grupami: kategoria od razu dostaje "Grupa A" (albo tyle grup, ile wybrał organizator).
            await defaultGroupProvisioner.EnsureDefaultGroupsAsync(tournament, [result.Value.Id], request.GroupCount, cancellationToken);

            return Result.Success(new AddTournamentCategoryResponse(result.Value.Id));
        }
    }
}
