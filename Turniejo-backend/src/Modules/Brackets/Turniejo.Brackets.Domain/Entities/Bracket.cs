using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Domain.Entities
{
    public sealed class Bracket : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public Guid CategoryId { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private Bracket() { }
        private Bracket(Guid id) : base(id) { }

        public static Result<Bracket> Create(Guid id, Guid tournamentId, Guid categoryId)
        {
            if (id == Guid.Empty)
                return Result.Failure<Bracket>(Error.Validation("Id", "Bracket id cannot be empty"));

            if (tournamentId == Guid.Empty)
                return Result.Failure<Bracket>(Error.Validation("TournamentId", "Bracket tournamentId cannot be empty"));

            if (categoryId == Guid.Empty)
                return Result.Failure<Bracket>(Error.Validation("CategoryId", "Bracket categoryId cannot be empty"));

            var bracket = new Bracket(id) { TournamentId = tournamentId, CategoryId = categoryId };

            bracket._domainEvents.Add(new BracketGenerated(bracket.Id, tournamentId));

            return Result.Success(bracket);
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
