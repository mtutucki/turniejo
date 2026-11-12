using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Pitches.AddPitch
{
    internal sealed class AddPitchCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext,
        ITournamentModule tournamentModule) : ICommandHandler<AddPitchCommand, AddPitchResponse>
    {
        public async Task<Result<AddPitchResponse>> Handle(AddPitchCommand request, CancellationToken cancellationToken)
        {
            var nameExists = await dbContext.PitchNameExistsAsync(request.TournamentId, request.Name, cancellationToken);
            if (nameExists)
                return Result.Failure<AddPitchResponse>(Error.Validation("Name", $"Pitch with name '{request.Name}' already exists in this tournament"));

            var groupIds = request.GroupIds ?? [];
            var groupsCheck = await PitchGroupsValidation.CheckAsync(tournamentModule, request.TournamentId, groupIds, cancellationToken);
            if (groupsCheck.IsFailure)
                return Result.Failure<AddPitchResponse>(groupsCheck.Error);

            var result = Pitch.Create(Guid.NewGuid(), request.TournamentId, request.Name, request.Description, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<AddPitchResponse>(result.Error);

            var pitch = result.Value;
            pitch.SetGroups(groupIds);

            await dbContext.AddPitchAsync(pitch, cancellationToken);

            foreach (var domainEvent in pitch.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddPitchResponse(pitch.Id));
        }
    }
}
