using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.TournamentGroups.UpdateTournamentGroup
{
    internal sealed class UpdateTournamentGroupCommandHandler(ITournamentsDbContext tournamentsDbContext) : ICommandHandler<UpdateTournamentGroupCommand, UpdateTournamentGroupResponse>
    {
        public async Task<Result<UpdateTournamentGroupResponse>> Handle(UpdateTournamentGroupCommand request, CancellationToken cancellationToken)
        {
            var group = await tournamentsDbContext.GetTournamentGroupByIdAsync(request.GroupId, cancellationToken);
            if (group == null || group.TournamentId != request.TournamentId)
                return Result.Failure<UpdateTournamentGroupResponse>(Error.NotFound("TournamentGroup", request.GroupId));

            // Unikalność nazwy w kategorii jak przy dodawaniu — ale sama zmiana kolejności nie może
            // odbić się od własnej, niezmienionej nazwy.
            var nameChanged = !string.Equals(group.Name, request.Name, StringComparison.Ordinal);
            if (nameChanged && await tournamentsDbContext.GroupNameExistsAsync(group.CategoryId, request.Name, cancellationToken))
                return Result.Failure<UpdateTournamentGroupResponse>(Error.Validation("Name", "Name already exists"));

            var updateResult = group.UpdateDetails(request.Name, request.Order);
            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentGroupResponse>(updateResult.Error);

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new UpdateTournamentGroupResponse(group.Id));
        }
    }
}
