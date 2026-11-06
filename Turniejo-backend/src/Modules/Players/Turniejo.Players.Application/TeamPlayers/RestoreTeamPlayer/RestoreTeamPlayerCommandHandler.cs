using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TeamPlayers.RestoreTeamPlayer
{
    internal sealed class RestoreTeamPlayerCommandHandler(
        IPlayersDbContext dbContext,
        TeamRosterAccessGuard accessGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<RestoreTeamPlayerCommand, RestoreTeamPlayerResponse>
    {
        public async Task<Result<RestoreTeamPlayerResponse>> Handle(RestoreTeamPlayerCommand request, CancellationToken cancellationToken)
        {
            var access = await accessGuard.GetAccessAsync(request.TeamId, currentUserContext.UserId, cancellationToken);
            if (access != TeamRosterAccess.Edit)
                throw new UnauthorizedException();

            var member = await dbContext.GetTeamPlayerAsync(request.TeamId, request.PlayerId, cancellationToken);
            if (member == null)
                return Result.Failure<RestoreTeamPlayerResponse>(Error.NotFound("Player", request.PlayerId));

            // Numer mógł w międzyczasie dostać ktoś inny: wtedy zawodnik wraca bez numeru.
            var members = await dbContext.GetTeamPlayersByTeamIdAsync(request.TeamId, cancellationToken);
            if (TeamRosterRules.IsShirtNumberTaken(members, member.ShirtNumber, exceptPlayerId: member.PlayerId))
                member.UpdateShirtNumber(null);

            member.Rejoin();

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new RestoreTeamPlayerResponse(member.PlayerId, member.ShirtNumber));
        }
    }
}
