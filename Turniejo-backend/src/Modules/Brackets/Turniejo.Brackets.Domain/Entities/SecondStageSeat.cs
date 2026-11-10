using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Domain.Entities
{
    /// <summary>
    /// Miejsce w grupie drugiej fazy (format „Dwie fazy grupowe”): kto tu trafi, mówi miejsce w tabeli
    /// grupy fazy 1 (np. 2. miejsce Grupy B). Drużyna jest znana dopiero po ostatnim meczu tej grupy.
    /// </summary>
    public sealed class SecondStageSeat : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public Guid CategoryId { get; private set; }
        public Guid GroupId { get; private set; }
        public int Order { get; private set; }
        public Guid SourceGroupId { get; private set; }
        public int SourcePosition { get; private set; }
        public Guid? TournamentTeamId { get; private set; }

        private SecondStageSeat() { }
        private SecondStageSeat(Guid id) : base(id) { }

        public static Result<SecondStageSeat> Create(
            Guid id, Guid tournamentId, Guid categoryId, Guid groupId, int order, Guid sourceGroupId, int sourcePosition)
        {
            if (id == Guid.Empty || tournamentId == Guid.Empty || categoryId == Guid.Empty || groupId == Guid.Empty || sourceGroupId == Guid.Empty)
                return Result.Failure<SecondStageSeat>(Error.Validation("Id", "Second-stage seat ids cannot be empty"));

            if (sourcePosition < 1)
                return Result.Failure<SecondStageSeat>(Error.Validation("SourcePosition", "Place in the group must be at least 1"));

            return Result.Success(new SecondStageSeat(id)
            {
                TournamentId = tournamentId,
                CategoryId = categoryId,
                GroupId = groupId,
                Order = order,
                SourceGroupId = sourceGroupId,
                SourcePosition = sourcePosition,
            });
        }

        public void Resolve(Guid? tournamentTeamId)
        {
            TournamentTeamId = tournamentTeamId;
            SetUpdatedAt();
        }
    }
}
