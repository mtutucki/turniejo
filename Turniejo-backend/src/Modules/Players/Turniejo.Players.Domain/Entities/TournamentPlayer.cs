using Turniejo.Players.Domain.Enums;
using Turniejo.Players.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Domain.Entities
{
    public sealed class TournamentPlayer : BaseEntity
    {
        public Guid TournamentTeamId { get; private set; }
        public Guid PlayerId { get; private set; }
        public int? ShirtNumber { get; private set; }
        public PlayerPosition? Position { get; private set; }
        public TournamentPlayerStatus Status { get; private set; } = TournamentPlayerStatus.Active;

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private TournamentPlayer() { }
        private TournamentPlayer(Guid id) : base(id) { }

        public static Result<TournamentPlayer> Create(
            Guid id, Guid tournamentTeamId, Guid playerId, int? shirtNumber, PlayerPosition? position, Guid assignedByUserId)
        {
            if (id == Guid.Empty)
                return Result.Failure<TournamentPlayer>(Error.Validation("Id", "TournamentPlayer id cannot be empty"));

            if (tournamentTeamId == Guid.Empty)
                return Result.Failure<TournamentPlayer>(Error.Validation("TournamentTeamId", "TournamentPlayer tournamentTeamId cannot be empty"));

            if (playerId == Guid.Empty)
                return Result.Failure<TournamentPlayer>(Error.Validation("PlayerId", "TournamentPlayer playerId cannot be empty"));

            if (shirtNumber is < 0)
                return Result.Failure<TournamentPlayer>(Error.Validation("ShirtNumber", "ShirtNumber cannot be negative"));

            var tournamentPlayer = new TournamentPlayer(id)
            {
                TournamentTeamId = tournamentTeamId,
                PlayerId = playerId,
                ShirtNumber = shirtNumber,
                Position = position
            };

            tournamentPlayer._domainEvents.Add(new PlayerAssignedToTeam(tournamentPlayer.Id, tournamentTeamId, playerId, assignedByUserId));

            return Result.Success(tournamentPlayer);
        }

        public Result UpdateDetails(int? shirtNumber, PlayerPosition? position, TournamentPlayerStatus status)
        {
            if (shirtNumber is < 0)
                return Result.Failure(Error.Validation("ShirtNumber", "ShirtNumber cannot be negative"));

            ShirtNumber = shirtNumber;
            Position = position;
            Status = status;
            SetUpdatedAt();

            _domainEvents.Add(new TournamentPlayerUpdated(Id, TournamentTeamId));

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
