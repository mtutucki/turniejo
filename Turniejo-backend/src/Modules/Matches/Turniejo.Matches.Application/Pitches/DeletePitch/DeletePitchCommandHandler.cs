using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Pitches.DeletePitch
{
    internal sealed class DeletePitchCommandHandler(IMatchesDbContext dbContext) : ICommandHandler<DeletePitchCommand>
    {
        public async Task<Result> Handle(DeletePitchCommand request, CancellationToken cancellationToken)
        {
            var pitch = await dbContext.GetPitchByIdAsync(request.PitchId, cancellationToken);
            if (pitch == null || pitch.TournamentId != request.TournamentId)
                return Result.Failure(Error.NotFound("Pitch", request.PitchId));

            // FK ma OnDelete SetNull — usunięcie boiska po cichu zostawiłoby zaplanowane na nim mecze bez boiska.
            if (await dbContext.AnyMatchesByPitchIdAsync(pitch.Id, cancellationToken))
                return Result.Failure(Error.Conflict("Pitch", "Cannot delete a pitch that has matches scheduled on it — deactivate it instead"));

            await dbContext.RemovePitchAsync(pitch, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
