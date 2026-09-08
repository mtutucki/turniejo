using Turniejo.Players.Domain.Enums;
using Turniejo.Players.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Domain.Entities
{
    public sealed class Player : BaseEntity
    {
        public Guid? UserId { get; private set; }
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public DateTime BirthDate { get; private set; }
        public PlayerPosition DefaultPosition { get; private set; }
        public string? PhotoUrl { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private Player() { }
        private Player(Guid id) : base(id) { }

        public static Result<Player> Create(
            Guid id, string firstName, string lastName, DateTime birthDate,
            PlayerPosition defaultPosition, string? photoUrl, Guid? userId, Guid createdByUserId)
        {
            if (id == Guid.Empty)
                return Result.Failure<Player>(Error.Validation("Id", "Player id cannot be empty"));

            if (string.IsNullOrWhiteSpace(firstName))
                return Result.Failure<Player>(Error.Validation("FirstName", "Player firstName cannot be empty"));

            if (string.IsNullOrWhiteSpace(lastName))
                return Result.Failure<Player>(Error.Validation("LastName", "Player lastName cannot be empty"));

            if (birthDate.Date > DateTime.UtcNow.Date)
                return Result.Failure<Player>(Error.Validation("BirthDate", "Player birthDate cannot be in the future"));

            var player = new Player(id)
            {
                UserId = userId,
                FirstName = firstName,
                LastName = lastName,
                BirthDate = birthDate,
                DefaultPosition = defaultPosition,
                PhotoUrl = photoUrl
            };

            player._domainEvents.Add(new PlayerCreated(player.Id, firstName, lastName, createdByUserId));

            return Result.Success(player);
        }

        public Result UpdateDetails(string firstName, string lastName, DateTime birthDate, PlayerPosition defaultPosition, string? photoUrl)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                return Result.Failure(Error.Validation("FirstName", "Player firstName cannot be empty"));

            if (string.IsNullOrWhiteSpace(lastName))
                return Result.Failure(Error.Validation("LastName", "Player lastName cannot be empty"));

            if (birthDate.Date > DateTime.UtcNow.Date)
                return Result.Failure(Error.Validation("BirthDate", "Player birthDate cannot be in the future"));

            FirstName = firstName;
            LastName = lastName;
            BirthDate = birthDate;
            DefaultPosition = defaultPosition;
            PhotoUrl = photoUrl;
            SetUpdatedAt();

            _domainEvents.Add(new PlayerUpdated(Id, firstName, lastName));

            return Result.Success();
        }

        public Result AssignUser(Guid userId)
        {
            if (userId == Guid.Empty)
                return Result.Failure(Error.Validation("UserId", "UserId cannot be empty"));

            UserId = userId;
            SetUpdatedAt();

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
