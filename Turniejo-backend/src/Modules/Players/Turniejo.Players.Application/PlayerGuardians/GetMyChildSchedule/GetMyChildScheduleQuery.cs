using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildSchedule
{
    // Handler weryfikuje GuardianAccessChecker.EnsureGuardianAsync(PlayerId, CurrentUserId) przed zwrotem danych.
    public sealed record GetMyChildScheduleQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildMatchDto>>, IAuthenticatedOnlyRequest;
}
