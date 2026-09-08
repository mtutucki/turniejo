using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildSchedule
{
    internal sealed class GetMyChildScheduleQueryHandler(
        MyChildMatchResolver matchResolver,
        GuardianAccessChecker accessChecker,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyChildScheduleQuery, IReadOnlyList<MyChildMatchDto>>
    {
        public async Task<Result<IReadOnlyList<MyChildMatchDto>>> Handle(GetMyChildScheduleQuery request, CancellationToken cancellationToken)
        {
            await accessChecker.EnsureGuardianAsync(request.PlayerId, currentUserContext.UserId, cancellationToken);

            var matches = await matchResolver.ResolveAsync(request.PlayerId, m => m.Status != "Finished", cancellationToken);

            return Result.Success(matches);
        }
    }
}
