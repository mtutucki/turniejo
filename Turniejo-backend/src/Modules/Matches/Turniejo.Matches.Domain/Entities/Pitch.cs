using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Domain.Entities
{
    public sealed class Pitch : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public bool IsActive { get; private set; } = true;

        // Grupy (faza 1 i 2), których mecze generator kładzie na tym boisku. Puste = wszystkie grupy.
        // Tablica uuid[] bez kluczy obcych: grupy żyją w module Tournaments.
        public List<Guid> GroupIds { get; private set; } = [];

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private Pitch() { }
        private Pitch(Guid id) : base(id) { }

        public static Result<Pitch> Create(Guid id, Guid tournamentId, string name, string? description, Guid addedByUserId)
        {
            if (id == Guid.Empty)
                return Result.Failure<Pitch>(Error.Validation("Id", "Pitch id cannot be empty"));

            if (tournamentId == Guid.Empty)
                return Result.Failure<Pitch>(Error.Validation("TournamentId", "Pitch tournamentId cannot be empty"));

            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<Pitch>(Error.Validation("Name", "Pitch name cannot be empty"));

            var pitch = new Pitch(id)
            {
                TournamentId = tournamentId,
                Name = name,
                Description = description,
                IsActive = true
            };

            pitch._domainEvents.Add(new PitchAdded(pitch.Id, tournamentId, name, addedByUserId));

            return Result.Success(pitch);
        }

        public Result UpdateDetails(string name, string? description, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(Error.Validation("Name", "Pitch name cannot be empty"));

            Name = name;
            Description = description;
            IsActive = isActive;
            SetUpdatedAt();

            return Result.Success();
        }

        public void SetGroups(IEnumerable<Guid> groupIds)
        {
            GroupIds = groupIds.Distinct().ToList();
            SetUpdatedAt();
        }

        public void RemoveGroup(Guid groupId)
        {
            if (GroupIds.Contains(groupId))
                SetGroups(GroupIds.Where(id => id != groupId));
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
