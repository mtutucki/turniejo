using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Domain.Entities
{
    /// <summary>
    /// Zakres miejsc z każdej grupy fazy 1, które trafiają do grupy fazy 2 (np. Grupa I: miejsca 1-2).
    /// Da się go ustawić, zanim grupy mają drużyny; konkretne miejsca (<see cref="SecondStageSeat"/>)
    /// powstają z zakresów przy generowaniu terminarza, z prawdziwej liczby drużyn w grupach.
    /// </summary>
    public sealed class SecondStageRange : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public Guid CategoryId { get; private set; }
        public Guid GroupId { get; private set; }
        public int FromPosition { get; private set; }
        public int ToPosition { get; private set; }

        private SecondStageRange() { }
        private SecondStageRange(Guid id) : base(id) { }

        public static Result<SecondStageRange> Create(
            Guid id, Guid tournamentId, Guid categoryId, Guid groupId, int fromPosition, int toPosition)
        {
            if (id == Guid.Empty || tournamentId == Guid.Empty || categoryId == Guid.Empty || groupId == Guid.Empty)
                return Result.Failure<SecondStageRange>(Error.Validation("Id", "Second-stage range ids cannot be empty"));

            if (fromPosition < 1 || toPosition < fromPosition)
                return Result.Failure<SecondStageRange>(Error.Validation("FromPosition", "A range of places must go from a lower place to a higher one"));

            return Result.Success(new SecondStageRange(id)
            {
                TournamentId = tournamentId,
                CategoryId = categoryId,
                GroupId = groupId,
                FromPosition = fromPosition,
                ToPosition = toPosition,
            });
        }
    }
}
