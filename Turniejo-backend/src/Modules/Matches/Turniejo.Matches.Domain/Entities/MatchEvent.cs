using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Domain.Entities
{
    public sealed class MatchEvent : BaseEntity
    {
        public Guid MatchId { get; private set; }
        public Guid? TournamentTeamId { get; private set; }
        public Guid? PlayerId { get; private set; }
        public Guid? RelatedPlayerId { get; private set; }
        public MatchEventType Type { get; private set; }
        public int Minute { get; private set; }
        public int? Second { get; private set; }
        public Guid CreatedByUserId { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTimeOffset? DeletedAt { get; private set; }
        public Guid? DeletedByUserId { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private MatchEvent() { }
        private MatchEvent(Guid id) : base(id) { }

        public static Result<MatchEvent> Create(
            Guid id, Guid matchId, MatchEventType type, int minute, Guid createdByUserId,
            Guid? tournamentTeamId = null, Guid? playerId = null, Guid? relatedPlayerId = null, int? second = null)
        {
            if (id == Guid.Empty)
                return Result.Failure<MatchEvent>(Error.Validation("Id", "MatchEvent id cannot be empty"));

            if (matchId == Guid.Empty)
                return Result.Failure<MatchEvent>(Error.Validation("MatchId", "MatchEvent matchId cannot be empty"));

            if (minute < 0)
                return Result.Failure<MatchEvent>(Error.Validation("Minute", "Minute cannot be negative"));

            if (second is < 0 or > 59)
                return Result.Failure<MatchEvent>(Error.Validation("Second", "Second must be between 0 and 59"));

            var matchEvent = new MatchEvent(id)
            {
                MatchId = matchId,
                TournamentTeamId = tournamentTeamId,
                PlayerId = playerId,
                RelatedPlayerId = relatedPlayerId,
                Type = type,
                Minute = minute,
                Second = second,
                CreatedByUserId = createdByUserId
            };

            return Result.Success(matchEvent);
        }

        public Result UpdateDetails(
            MatchEventType type, int minute, int? second, Guid? playerId, Guid? relatedPlayerId,
            Guid changedByUserId, Guid tournamentId)
        {
            if (IsDeleted)
                return Result.Failure(Error.Conflict("IsDeleted", "Cannot update a reverted event"));

            if (minute < 0)
                return Result.Failure(Error.Validation("Minute", "Minute cannot be negative"));

            if (second is < 0 or > 59)
                return Result.Failure(Error.Validation("Second", "Second must be between 0 and 59"));

            Type = type;
            Minute = minute;
            Second = second;
            PlayerId = playerId;
            RelatedPlayerId = relatedPlayerId;
            SetUpdatedAt();

            _domainEvents.Add(new MatchEventUpdated(Id, MatchId, tournamentId, changedByUserId));

            return Result.Success();
        }

        public Result MarkDeleted(Guid deletedByUserId, Guid tournamentId)
        {
            if (IsDeleted)
                return Result.Failure(Error.Conflict("IsDeleted", "Event is already reverted"));

            IsDeleted = true;
            DeletedAt = DateTimeOffset.UtcNow;
            DeletedByUserId = deletedByUserId;
            SetUpdatedAt();

            _domainEvents.Add(new MatchEventReverted(Id, MatchId, tournamentId, deletedByUserId));

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
