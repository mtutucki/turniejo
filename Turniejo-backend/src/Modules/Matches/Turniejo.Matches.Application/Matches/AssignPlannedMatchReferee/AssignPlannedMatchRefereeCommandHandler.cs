using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AssignPlannedMatchReferee
{
    internal sealed class AssignPlannedMatchRefereeCommandHandler(
        PlannedMatchContext plannedMatchContext,
        IAuthorizationService authorizationService,
        IBracketModule bracketModule) : ICommandHandler<AssignPlannedMatchRefereeCommand, AssignPlannedMatchRefereeResponse>
    {
        public async Task<Result<AssignPlannedMatchRefereeResponse>> Handle(AssignPlannedMatchRefereeCommand request, CancellationToken cancellationToken)
        {
            var loaded = await plannedMatchContext.LoadAsync(request.TournamentId, request.NodeId, cancellationToken);
            if (loaded.IsFailure)
                return Result.Failure<AssignPlannedMatchRefereeResponse>(loaded.Error);
            var ctx = loaded.Value;

            if (request.RefereeId is { } refereeId)
            {
                // Rolę sędziego w meczu nadajemy dopiero, gdy mecz powstanie. Tu tylko pilnujemy, żeby
                // to był sędzia tego turnieju (tak jak przy zwykłym meczu).
                var isTournamentReferee = await authorizationService.HasAnyRoleAsync(
                    refereeId, nameof(RoleScopeType.Tournament), request.TournamentId, [nameof(UserRole.Referee)], cancellationToken);
                if (!isTournamentReferee)
                    return Result.Failure<AssignPlannedMatchRefereeResponse>(Error.Validation("RefereeId", "User is not a referee in this tournament"));

                if (ctx.Node.PlannedAt is { } start && refereeId != ctx.Node.PlannedRefereeId &&
                    PlannedMatchContext.RefereeBusy(ctx, refereeId, start))
                    return Result.Failure<AssignPlannedMatchRefereeResponse>(Error.Validation("RefereeId", "The referee already has another match at this time"));
            }

            await bracketModule.SetPlannedRefereeAsync(ctx.Node.Id, request.RefereeId, cancellationToken);

            return Result.Success(new AssignPlannedMatchRefereeResponse(ctx.Node.Id));
        }
    }
}
