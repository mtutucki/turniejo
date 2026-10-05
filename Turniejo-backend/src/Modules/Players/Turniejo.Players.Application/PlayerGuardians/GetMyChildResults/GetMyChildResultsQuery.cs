using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildResults
{
    // Handler weryfikuje GuardianAccessChecker.EnsureGuardianAsync(PlayerId, CurrentUserId) przed zwrotem danych.
    public sealed record GetMyChildResultsQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildMatchDto>>, IAuthenticatedOnlyRequest;
}
