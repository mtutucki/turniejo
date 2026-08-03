using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.Application.Tournaments.CreateTournament
{
    internal sealed class CreateTournamentCommandHandler(ITournamentsDbContext dbContext, ICurrentUserContext currentUserContext, IUserModule userModule) : ICommandHandler<CreateTournamentCommand, CreateTournamentResponse>
    {
        public async Task<Result<CreateTournamentResponse>> Handle(CreateTournamentCommand request, CancellationToken cancellationToken)
        {
            var result = Tournament.Create(
                Guid.NewGuid(),
                request.OrganizationId,
                currentUserContext.UserId,
                request.Name,
                request.StartAt,
                request.EndAt,
                request.TeamLimit,
                request.Description);

            if (result.IsFailure)
                return Result.Failure<CreateTournamentResponse>(result.Error);

            var tournament = result.Value;

            bool isSlugExists = await dbContext.TournamentSlugExistsAsync(tournament.Slug, cancellationToken);

            if (isSlugExists)
                return Result.Failure<CreateTournamentResponse>(new Error("409", "Slug already exists"));

            await dbContext.AddTournamentAsync(tournament, cancellationToken);

            var outboxMessage = OutboxMessage.Create(
                typeof(TournamentCreated).AssemblyQualifiedName!,
                JsonSerializer.Serialize(new TournamentCreated(
                    tournament.Id, tournament.OrganizationId, tournament.CreatedByUserId, tournament.Name, tournament.StartAt, tournament.EndAt)
                ));

            await userModule.AssignRoleAsync(
                currentUserContext.UserId,
                nameof(UserRole.Organizer),
                nameof(RoleScopeType.Tournament),
                tournament.Id,
                cancellationToken);

            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateTournamentResponse(tournament.Id, tournament.Slug));

        }
    }
}
