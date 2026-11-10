using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Domain.Entities
{
    /// <summary>
    /// Mecz grupy drugiej fazy zaplanowany przez generator terminarza, zanim znane są drużyny
    /// (np. „1. miejsce Gr. A – 2. miejsce Gr. B”). Mecz (Match) powstaje w tym terminie, gdy oba
    /// miejsca mają drużyny - tak samo jak kolejne rundy pucharu na węźle drabinki.
    /// </summary>
    public sealed class SecondStageFixture : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public Guid CategoryId { get; private set; }
        public Guid GroupId { get; private set; }
        public Guid HomeSeatId { get; private set; }
        public Guid AwaySeatId { get; private set; }
        public int Round { get; private set; }
        public Guid? PlannedPitchId { get; private set; }
        public DateTimeOffset? PlannedAt { get; private set; }
        public Guid? PlannedRefereeId { get; private set; }
        public Guid? MatchId { get; private set; }

        private SecondStageFixture() { }
        private SecondStageFixture(Guid id) : base(id) { }

        public static Result<SecondStageFixture> Create(
            Guid id, Guid tournamentId, Guid categoryId, Guid groupId, Guid homeSeatId, Guid awaySeatId, int round,
            Guid pitchId, DateTimeOffset plannedAt)
        {
            if (id == Guid.Empty || tournamentId == Guid.Empty || groupId == Guid.Empty || homeSeatId == Guid.Empty || awaySeatId == Guid.Empty)
                return Result.Failure<SecondStageFixture>(Error.Validation("Id", "Second-stage fixture ids cannot be empty"));

            if (homeSeatId == awaySeatId)
                return Result.Failure<SecondStageFixture>(Error.Validation("AwaySeatId", "A place cannot play against itself"));

            return Result.Success(new SecondStageFixture(id)
            {
                TournamentId = tournamentId,
                CategoryId = categoryId,
                GroupId = groupId,
                HomeSeatId = homeSeatId,
                AwaySeatId = awaySeatId,
                Round = round,
                PlannedPitchId = pitchId,
                PlannedAt = plannedAt,
            });
        }

        public void PlanSlot(Guid pitchId, DateTimeOffset plannedAt)
        {
            PlannedPitchId = pitchId;
            PlannedAt = plannedAt;
            SetUpdatedAt();
        }

        public void PlanReferee(Guid? refereeId)
        {
            PlannedRefereeId = refereeId;
            SetUpdatedAt();
        }

        public Result AssignMatch(Guid matchId)
        {
            if (MatchId.HasValue)
                return Result.Failure(Error.Conflict("MatchId", "This fixture already has a match"));

            MatchId = matchId;
            SetUpdatedAt();
            return Result.Success();
        }
    }
}
