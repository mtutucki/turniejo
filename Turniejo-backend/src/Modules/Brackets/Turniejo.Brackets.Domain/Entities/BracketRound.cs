using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Domain.Entities
{
    public sealed class BracketRound : BaseEntity
    {
        public Guid BracketId { get; private set; }
        public int RoundNumber { get; private set; }
        public BracketRoundType RoundType { get; private set; }
        public string Name { get; private set; } = string.Empty;

        private BracketRound() { }
        private BracketRound(Guid id) : base(id) { }

        public static Result<BracketRound> Create(Guid id, Guid bracketId, int roundNumber, BracketRoundType roundType, string name)
        {
            if (id == Guid.Empty)
                return Result.Failure<BracketRound>(Error.Validation("Id", "BracketRound id cannot be empty"));

            if (bracketId == Guid.Empty)
                return Result.Failure<BracketRound>(Error.Validation("BracketId", "BracketRound bracketId cannot be empty"));

            if (roundNumber <= 0)
                return Result.Failure<BracketRound>(Error.Validation("RoundNumber", "RoundNumber must be positive"));

            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<BracketRound>(Error.Validation("Name", "BracketRound name cannot be empty"));

            var round = new BracketRound(id)
            {
                BracketId = bracketId,
                RoundNumber = roundNumber,
                RoundType = roundType,
                Name = name
            };

            return Result.Success(round);
        }
    }
}
