using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TournamentTeams.AssignTeamToGroup
{
    // GroupId nie ma FK do Tournaments (moduły nie dzielą DbContextu ani schematu), więc bez tej
    // kontroli dało się przypisać drużynę do nieistniejącego GUID-a grupy — drużyna cicho znikała
    // z fazy grupowej (RecalculateStandingsCommandHandler filtruje po GroupId) bez żadnego błędu.
    internal sealed class AssignTeamToGroupCommandHandler(
        ITeamsDbContext dbContext,
        ITournamentModule tournamentModule) : ICommandHandler<AssignTeamToGroupCommand, AssignTeamToGroupResponse>
    {
        public async Task<Result<AssignTeamToGroupResponse>> Handle(AssignTeamToGroupCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<AssignTeamToGroupResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            if (request.GroupId.HasValue)
            {
                var group = await tournamentModule.GetTournamentGroupAsync(request.GroupId.Value, cancellationToken);
                if (group == null)
                    return Result.Failure<AssignTeamToGroupResponse>(Error.NotFound("TournamentGroup", request.GroupId.Value));

                if (group.TournamentId != request.TournamentId)
                    return Result.Failure<AssignTeamToGroupResponse>(Error.Validation("GroupId", "Group does not belong to this tournament"));

                if (group.CategoryId != tournamentTeam.CategoryId)
                    return Result.Failure<AssignTeamToGroupResponse>(Error.Validation("GroupId", "Group does not belong to the team's category"));
            }

            var assignResult = tournamentTeam.AssignToGroup(request.GroupId);
            if (assignResult.IsFailure)
                return Result.Failure<AssignTeamToGroupResponse>(assignResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignTeamToGroupResponse(tournamentTeam.Id));
        }
    }
}
