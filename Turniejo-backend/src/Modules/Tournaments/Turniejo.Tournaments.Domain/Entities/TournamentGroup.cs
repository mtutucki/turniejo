using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.Domain.Entities
{
    public sealed class TournamentGroup : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public Guid CategoryId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int Order { get; private set; }
        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private TournamentGroup() { }

        private TournamentGroup(Guid id) : base(id) { }


        public static Result<TournamentGroup> Create(Guid id, Guid organizationId, Guid tournamentId, Guid createdByUserId, Guid categoryId, string name, int order)
        {
            if (id == Guid.Empty)
            {
                return Result.Failure<TournamentGroup>(Error.Validation("Id", "TournamentGroup ID cannot be empty"));
            }

            if (organizationId == Guid.Empty)
            {
                return Result.Failure<TournamentGroup>(Error.Validation("OrganizationId", "TournamentGroup organizationId cannot be empty"));
            }

            if (tournamentId == Guid.Empty)
            {
                return Result.Failure<TournamentGroup>(Error.Validation("TournamentId", "TournamentGroup tournamentId cannot be empty"));
            }

            if (createdByUserId == Guid.Empty)
            {
                return Result.Failure<TournamentGroup>(Error.Validation("CreatedByUserId", "TournamentGroup createdByUserId cannot be empty"));
            }

            if (categoryId == Guid.Empty)
            {
                return Result.Failure<TournamentGroup>(Error.Validation("CategoryId", "TournamentGroup categoryId cannot be empty"));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure<TournamentGroup>(Error.Validation("Name", "TournamentGroup name cannot be empty"));
            }

            TournamentGroup group = new TournamentGroup(id)
            {
                TournamentId = tournamentId,
                CategoryId = categoryId,
                Name = name,
                Order = order
            };

            group._domainEvents.Add(new TournamentGroupAdded(
                group.TournamentId,
                group.Id,
                organizationId,
                createdByUserId,
                group.Name
            ));


            return Result.Success(group);
        }

    }
}
