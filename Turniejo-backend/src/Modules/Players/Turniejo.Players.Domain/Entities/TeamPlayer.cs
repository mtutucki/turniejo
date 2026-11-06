using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Domain.Entities
{
    // Kadra drużyny: zawodnik należy do drużyny niezależnie od turniejów. Składy turniejowe (TournamentPlayer)
    // wybiera się z kadry. Usunięcie z kadry nie kasuje profilu, tylko zmienia status na Left.
    public sealed class TeamPlayer : BaseEntity
    {
        public Guid TeamId { get; private set; }
        public Guid PlayerId { get; private set; }
        // Domyślny numer podpowiadany przy dodawaniu do składu turniejowego.
        public int? ShirtNumber { get; private set; }
        public TeamPlayerStatus Status { get; private set; } = TeamPlayerStatus.Active;
        public DateTimeOffset? LeftAt { get; private set; }

        private TeamPlayer() { }
        private TeamPlayer(Guid id) : base(id) { }

        public static Result<TeamPlayer> Create(Guid id, Guid teamId, Guid playerId, int? shirtNumber)
        {
            if (id == Guid.Empty)
                return Result.Failure<TeamPlayer>(Error.Validation("Id", "TeamPlayer id cannot be empty"));

            if (teamId == Guid.Empty)
                return Result.Failure<TeamPlayer>(Error.Validation("TeamId", "TeamPlayer teamId cannot be empty"));

            if (playerId == Guid.Empty)
                return Result.Failure<TeamPlayer>(Error.Validation("PlayerId", "TeamPlayer playerId cannot be empty"));

            if (shirtNumber is < 1)
                return Result.Failure<TeamPlayer>(Error.Validation("ShirtNumber", "ShirtNumber must be greater than 0"));

            return Result.Success(new TeamPlayer(id)
            {
                TeamId = teamId,
                PlayerId = playerId,
                ShirtNumber = shirtNumber
            });
        }

        public Result UpdateShirtNumber(int? shirtNumber)
        {
            if (shirtNumber is < 1)
                return Result.Failure(Error.Validation("ShirtNumber", "ShirtNumber must be greater than 0"));

            ShirtNumber = shirtNumber;
            SetUpdatedAt();
            return Result.Success();
        }

        public void Leave()
        {
            if (Status == TeamPlayerStatus.Left)
                return;

            Status = TeamPlayerStatus.Left;
            LeftAt = DateTimeOffset.UtcNow;
            SetUpdatedAt();
        }

        public void Rejoin()
        {
            if (Status == TeamPlayerStatus.Active)
                return;

            Status = TeamPlayerStatus.Active;
            LeftAt = null;
            SetUpdatedAt();
        }
    }
}
