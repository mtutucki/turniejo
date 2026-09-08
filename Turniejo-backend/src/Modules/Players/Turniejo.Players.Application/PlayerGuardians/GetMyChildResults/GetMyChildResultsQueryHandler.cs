using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildResults
{
    internal sealed class GetMyChildResultsQueryHandler(
        MyChildMatchResolver matchResolver,
        GuardianAccessChecker accessChecker,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyChildResultsQuery, IReadOnlyList<MyChildMatchDto>>
    {
        public async Task<Result<IReadOnlyList<MyChildMatchDto>>> Handle(GetMyChildResultsQuery request, CancellationToken cancellationToken)
        {
            await accessChecker.EnsureGuardianAsync(request.PlayerId, currentUserContext.UserId, cancellationToken);

            var matches = await matchResolver.ResolveAsync(request.PlayerId, m => m.Status == "Finished", cancellationToken);

            return Result.Success(matches);
        }
    }
}
