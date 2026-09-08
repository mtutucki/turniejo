using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Pitches.UpdatePitch
{
    internal sealed class UpdatePitchCommandHandler(IMatchesDbContext dbContext) : ICommandHandler<UpdatePitchCommand, UpdatePitchResponse>
    {
        public async Task<Result<UpdatePitchResponse>> Handle(UpdatePitchCommand request, CancellationToken cancellationToken)
        {
            var pitch = await dbContext.GetPitchByIdAsync(request.PitchId, cancellationToken);
            if (pitch == null || pitch.TournamentId != request.TournamentId)
                return Result.Failure<UpdatePitchResponse>(Error.NotFound("Pitch", request.PitchId));

            if (!string.Equals(pitch.Name, request.Name, StringComparison.Ordinal))
            {
                var nameExists = await dbContext.PitchNameExistsAsync(request.TournamentId, request.Name, cancellationToken);
                if (nameExists)
                    return Result.Failure<UpdatePitchResponse>(Error.Validation("Name", $"Pitch with name '{request.Name}' already exists in this tournament"));
            }

            var updateResult = pitch.UpdateDetails(request.Name, request.Description, request.IsActive);
            if (updateResult.IsFailure)
                return Result.Failure<UpdatePitchResponse>(updateResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdatePitchResponse(pitch.Id));
        }
    }
}
