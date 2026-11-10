using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Domain.Entities
{
    public sealed class TieBreakerRule : BaseEntity
    {
        /// <summary>
        /// Kolejność używana, dopóki organizator nie zapisze własnej. Bez niej remis punktowy
        /// nie byłby rozstrzygany niczym sensownym.
        /// </summary>
        public static readonly IReadOnlyList<TieBreakerType> DefaultOrder =
        [
            TieBreakerType.HeadToHeadPoints,
            TieBreakerType.HeadToHeadGoalDiff,
            TieBreakerType.GoalDifference,
            TieBreakerType.GoalsFor,
            TieBreakerType.Wins,
        ];

        public Guid TournamentId { get; private set; }
        public int Order { get; private set; }
        public TieBreakerType Type { get; private set; }

        private TieBreakerRule() { }

        private TieBreakerRule(Guid id) : base(id) { }

        public static Result<TieBreakerRule> Create(Guid id, Guid tournamentId, int order, TieBreakerType type)
        {
            if (id == Guid.Empty)
                return Result.Failure<TieBreakerRule>(Error.Validation("Id", "TieBreakerRule id cannot be empty"));
            if (tournamentId == Guid.Empty)
                return Result.Failure<TieBreakerRule>(Error.Validation("TournamentId", "TieBreakerRule tournamentId cannot be empty"));

            TieBreakerRule tieBreakerRule = new TieBreakerRule(id)
            {
                TournamentId = tournamentId,
                Order = order,
                Type = type
            };
            return Result.Success(tieBreakerRule);
        }
    }
}
