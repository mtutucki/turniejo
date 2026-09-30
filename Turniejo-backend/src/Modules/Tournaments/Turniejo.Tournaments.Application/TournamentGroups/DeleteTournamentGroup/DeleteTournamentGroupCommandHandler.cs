using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.TournamentGroups.DeleteTournamentGroup
{
    // GroupId w Teams/Matches nie ma FK (osobne moduły) — bez tych kontroli skasowanie grupy cicho
    // osierocałoby przypisane drużyny i mecze, psując tabele grupowe.
    internal sealed class DeleteTournamentGroupCommandHandler(
        ITournamentsDbContext tournamentsDbContext,
        ITeamModule teamModule,
        IMatchModule matchModule) : ICommandHandler<DeleteTournamentGroupCommand>
    {
        public async Task<Result> Handle(DeleteTournamentGroupCommand request, CancellationToken cancellationToken)
        {
            var group = await tournamentsDbContext.GetTournamentGroupByIdAsync(request.GroupId, cancellationToken);
            if (group == null || group.TournamentId != request.TournamentId)
                return Result.Failure(Error.NotFound("TournamentGroup", request.GroupId));

            if (await teamModule.AnyTeamsInGroupAsync(request.GroupId, cancellationToken))
                return Result.Failure(Error.Conflict("GroupId", "Cannot delete a group that still has teams assigned to it"));

            if (await matchModule.AnyMatchesForGroupAsync(request.GroupId, cancellationToken))
                return Result.Failure(Error.Conflict("GroupId", "Cannot delete a group that still has matches scheduled in it"));

            await tournamentsDbContext.RemoveTournamentGroupAsync(group, cancellationToken);
            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
