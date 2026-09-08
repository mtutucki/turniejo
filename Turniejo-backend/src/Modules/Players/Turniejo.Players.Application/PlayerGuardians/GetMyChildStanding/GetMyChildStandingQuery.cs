using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildStanding
{
    // Handler weryfikuje GuardianAccessChecker.EnsureGuardianAsync(PlayerId, CurrentUserId) przed zwrotem danych.
    public sealed record GetMyChildStandingQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildStandingDto>>, IAuthenticatedOnlyRequest;
}
