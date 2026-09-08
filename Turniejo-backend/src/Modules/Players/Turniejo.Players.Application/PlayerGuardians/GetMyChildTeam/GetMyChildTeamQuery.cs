using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildTeam
{
    // Handler weryfikuje GuardianAccessChecker.EnsureGuardianAsync(PlayerId, CurrentUserId) przed zwrotem danych.
    public sealed record GetMyChildTeamQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildTeamDto>>, IAuthenticatedOnlyRequest;
}
