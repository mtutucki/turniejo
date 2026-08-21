using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.Domain.Entities
{
    public sealed class TournamentCategory : BaseEntity
    {

        public Guid TournamentId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public DateTime? MinBirthDate { get; private set; }
        public DateTime? MaxBirthDate { get; private set; }
        public int Order { get; private set; }
        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
        private TournamentCategory() { }
        private TournamentCategory(Guid id) : base(id) { }

        public static Result<TournamentCategory> Create(Guid id, Guid organizationId, Guid tournamentId, Guid createdByUserId, string name, DateTime? minBirthDate, DateTime? maxBirthDate, int order)
        {
            if (id == Guid.Empty)
                return Result.Failure<TournamentCategory>(Error.Validation("Id", "TournamentCategory id cannot be empty"));

            if (organizationId == Guid.Empty)
                return Result.Failure<TournamentCategory>(Error.Validation("OrganizationId", "TournamentCategory organizationId cannot be empty"));

            if (createdByUserId == Guid.Empty)
                return Result.Failure<TournamentCategory>(Error.Validation("CreatedByUserId", "TournamentCategory createdByUserId cannot be empty"));

            if (tournamentId == Guid.Empty)
                return Result.Failure<TournamentCategory>(Error.Validation("TournamentId", "TournamentCategory tournamentId cannot be empty"));

            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<TournamentCategory>(Error.Validation("Name", "TournamentCategory name cannot be empty"));

            if (minBirthDate > maxBirthDate)
                return Result.Failure<TournamentCategory>(Error.Validation("BirthDate", "TournamentCategory minBirthDate cannot be greater than maxBirthDate"));


            TournamentCategory tournamentCategory = new TournamentCategory(id)
            {
                TournamentId = tournamentId,
                Name = name,
                MinBirthDate = minBirthDate,
                MaxBirthDate = maxBirthDate,
                Order = order
            };

            tournamentCategory._domainEvents.Add(new TournamentCategoryAdded(
                tournamentId,
                tournamentCategory.Id,
                organizationId,
                createdByUserId,
                name
             ));

            return Result.Success(tournamentCategory);
        }

        public Result UpdateDetails(string name, DateTime? minBirthDate, DateTime? maxBirthDate, int order)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(Error.Validation("Name", "TournamentCategory name cannot be empty"));
            if (minBirthDate > maxBirthDate)
                return Result.Failure(Error.Validation("BirthDate", "TournamentCategory minBirthDate cannot be greater than maxBirthDate"));
            Name = name;
            MinBirthDate = minBirthDate;
            MaxBirthDate = maxBirthDate;
            Order = order;

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
