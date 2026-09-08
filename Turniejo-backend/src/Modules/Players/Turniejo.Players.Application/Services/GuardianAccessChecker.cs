using Turniejo.Players.Application.Abstractions;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.Application.Services
{
    public sealed class GuardianAccessChecker(IPlayersDbContext dbContext)
    {
        public async Task EnsureGuardianAsync(Guid playerId, Guid userId, CancellationToken cancellationToken = default)
        {
            var guardians = await dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, cancellationToken);
            if (!guardians.Any(g => g.UserId == userId))
                throw new UnauthorizedException();
        }
    }
}
