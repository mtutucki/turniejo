using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Services
{
    // Kto trafia do składu turniejowego, ten jest w kadrze drużyny. Dopisuje brakujących i przywraca tych,
    // którzy odeszli. Zmiany są śledzone, zapisuje je SaveChanges wywołującego (ta sama transakcja co skład).
    public sealed class TeamRosterSync(IPlayersDbContext dbContext)
    {
        public async Task<Result> EnsureInRosterAsync(
            Guid teamId, IEnumerable<(Guid PlayerId, int? ShirtNumber)> players, CancellationToken cancellationToken = default)
        {
            var handled = new HashSet<Guid>();
            foreach (var (playerId, shirtNumber) in players)
            {
                if (!handled.Add(playerId))
                    continue;

                var member = await dbContext.GetTeamPlayerAsync(teamId, playerId, cancellationToken);
                if (member != null)
                {
                    member.Rejoin();
                    continue;
                }

                var created = TeamPlayer.Create(Guid.NewGuid(), teamId, playerId, shirtNumber is > 0 ? shirtNumber : null);
                if (created.IsFailure)
                    return Result.Failure(created.Error);

                await dbContext.AddTeamPlayerAsync(created.Value, cancellationToken);
            }

            return Result.Success();
        }
    }
}
