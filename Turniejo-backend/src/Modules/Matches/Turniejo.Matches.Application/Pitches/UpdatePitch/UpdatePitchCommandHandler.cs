using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Pitches.UpdatePitch
{
    internal sealed class UpdatePitchCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        IAuthorizationService authorizationService,
        PitchRefereeAssigner pitchRefereeAssigner,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdatePitchCommand, UpdatePitchResponse>
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

            var refereeCheck = await PitchRefereeValidation.CheckAsync(
                dbContext, authorizationService, request.TournamentId, pitch.Id, request.RefereeId, cancellationToken);
            if (refereeCheck.IsFailure)
                return Result.Failure<UpdatePitchResponse>(refereeCheck.Error);

            var updateResult = pitch.UpdateDetails(request.Name, request.Description, request.IsActive);
            if (updateResult.IsFailure)
                return Result.Failure<UpdatePitchResponse>(updateResult.Error);

            // Brak listy w żądaniu = grupy bez zmian (starszy klient).
            if (request.GroupIds is { } groupIds)
            {
                var groupsCheck = await PitchGroupsValidation.CheckAsync(tournamentModule, request.TournamentId, groupIds, cancellationToken);
                if (groupsCheck.IsFailure)
                    return Result.Failure<UpdatePitchResponse>(groupsCheck.Error);

                pitch.SetGroups(groupIds);
            }

            // Nowy sędzia boiska przejmuje jego nierozpoczęte mecze bez sędziego i te, które prowadził
            // poprzedni sędzia boiska; bez sędziego boiska te ostatnie wracają do puli.
            var previousReferee = pitch.RefereeId;
            if (request.RefereeId != previousReferee)
            {
                pitch.SetReferee(request.RefereeId);
                var onPitch = (await dbContext.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken))
                    .Where(m => m.PitchId == pitch.Id)
                    .Select(m => m.Id)
                    .ToList();
                var matches = onPitch.Count > 0
                    ? await dbContext.GetTrackedMatchesByIdsAsync(onPitch, cancellationToken)
                    : [];
                await pitchRefereeAssigner.ApplyAsync(
                    matches.Select(m => new PitchRefereeTarget(m, request.RefereeId, previousReferee)).ToList(),
                    currentUserContext.UserId, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdatePitchResponse(pitch.Id));
        }
    }
}
