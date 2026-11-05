using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Domain.Entities
{
    public sealed class BracketNode : BaseEntity
    {
        public Guid BracketRoundId { get; private set; }
        public Guid? MatchId { get; private set; }

        public BracketSourceType HomeSourceType { get; private set; }
        public Guid? HomeSourceGroupId { get; private set; }
        public int? HomeSourcePosition { get; private set; }
        public Guid? HomeSourceNodeId { get; private set; }
        public Guid? HomeTournamentTeamId { get; private set; }

        public BracketSourceType AwaySourceType { get; private set; }
        public Guid? AwaySourceGroupId { get; private set; }
        public int? AwaySourcePosition { get; private set; }
        public Guid? AwaySourceNodeId { get; private set; }
        public Guid? AwayTournamentTeamId { get; private set; }

        public Guid? WinnerGoesToNodeId { get; private set; }
        public BracketSlot? WinnerGoesToSlot { get; private set; }
        public Guid? LoserGoesToNodeId { get; private set; }
        public BracketSlot? LoserGoesToSlot { get; private set; }

        // Termin zaplanowany przez generator terminarza dla meczu, który jeszcze nie istnieje (drużyny
        // wyłoni poprzednia runda). Mecz powstaje potem w tym terminie.
        public Guid? PlannedPitchId { get; private set; }
        public DateTimeOffset? PlannedAt { get; private set; }
        // Sędzia wskazany z góry przez organizatora — mecz dostaje go, gdy powstanie.
        public Guid? PlannedRefereeId { get; private set; }

        public bool IsFullyResolved => HomeTournamentTeamId.HasValue && AwayTournamentTeamId.HasValue;

        private BracketNode() { }
        private BracketNode(Guid id) : base(id) { }

        public static Result<BracketNode> Create(
            Guid id, Guid bracketRoundId,
            BracketSourceType homeSourceType, Guid? homeSourceGroupId, int? homeSourcePosition, Guid? homeSourceNodeId, Guid? homeTournamentTeamId,
            BracketSourceType awaySourceType, Guid? awaySourceGroupId, int? awaySourcePosition, Guid? awaySourceNodeId, Guid? awayTournamentTeamId)
        {
            if (id == Guid.Empty)
                return Result.Failure<BracketNode>(Error.Validation("Id", "BracketNode id cannot be empty"));

            if (bracketRoundId == Guid.Empty)
                return Result.Failure<BracketNode>(Error.Validation("BracketRoundId", "BracketNode bracketRoundId cannot be empty"));

            if (homeSourceType == BracketSourceType.Fixed && !homeTournamentTeamId.HasValue)
                return Result.Failure<BracketNode>(Error.Validation("HomeTournamentTeamId", "Fixed home source requires a tournamentTeamId"));

            if (homeSourceType == BracketSourceType.GroupPosition && (!homeSourceGroupId.HasValue || !homeSourcePosition.HasValue))
                return Result.Failure<BracketNode>(Error.Validation("HomeSourceGroupId", "GroupPosition home source requires groupId and position"));

            if (homeSourceType is BracketSourceType.MatchWinner or BracketSourceType.MatchLoser && !homeSourceNodeId.HasValue)
                return Result.Failure<BracketNode>(Error.Validation("HomeSourceNodeId", "MatchWinner/MatchLoser home source requires a source node"));

            if (awaySourceType == BracketSourceType.Fixed && !awayTournamentTeamId.HasValue)
                return Result.Failure<BracketNode>(Error.Validation("AwayTournamentTeamId", "Fixed away source requires a tournamentTeamId"));

            if (awaySourceType == BracketSourceType.GroupPosition && (!awaySourceGroupId.HasValue || !awaySourcePosition.HasValue))
                return Result.Failure<BracketNode>(Error.Validation("AwaySourceGroupId", "GroupPosition away source requires groupId and position"));

            if (awaySourceType is BracketSourceType.MatchWinner or BracketSourceType.MatchLoser && !awaySourceNodeId.HasValue)
                return Result.Failure<BracketNode>(Error.Validation("AwaySourceNodeId", "MatchWinner/MatchLoser away source requires a source node"));

            var node = new BracketNode(id)
            {
                BracketRoundId = bracketRoundId,
                HomeSourceType = homeSourceType,
                HomeSourceGroupId = homeSourceGroupId,
                HomeSourcePosition = homeSourcePosition,
                HomeSourceNodeId = homeSourceNodeId,
                HomeTournamentTeamId = homeTournamentTeamId,
                AwaySourceType = awaySourceType,
                AwaySourceGroupId = awaySourceGroupId,
                AwaySourcePosition = awaySourcePosition,
                AwaySourceNodeId = awaySourceNodeId,
                AwayTournamentTeamId = awayTournamentTeamId
            };

            return Result.Success(node);
        }

        public void WireAdvancement(Guid? winnerGoesToNodeId, BracketSlot? winnerGoesToSlot, Guid? loserGoesToNodeId, BracketSlot? loserGoesToSlot)
        {
            WinnerGoesToNodeId = winnerGoesToNodeId;
            WinnerGoesToSlot = winnerGoesToSlot;
            LoserGoesToNodeId = loserGoesToNodeId;
            LoserGoesToSlot = loserGoesToSlot;
            SetUpdatedAt();
        }

        public Result ResolveSlot(BracketSlot slot, Guid tournamentTeamId)
        {
            if (tournamentTeamId == Guid.Empty)
                return Result.Failure(Error.Validation("TournamentTeamId", "TournamentTeamId cannot be empty"));

            if (slot == BracketSlot.Home)
                HomeTournamentTeamId = tournamentTeamId;
            else
                AwayTournamentTeamId = tournamentTeamId;

            SetUpdatedAt();

            return Result.Success();
        }

        // Źródło slotu pierwszej rundy (drużyna, miejsce w grupie albo wolny los) — do zamiany par
        // przez organizatora. Sloty kolejnych rund mają jeszcze HomeSourceNodeId/AwaySourceNodeId
        // i nie podlegają zamianie.
        public BracketSlotSource GetSlotSource(BracketSlot slot) =>
            slot == BracketSlot.Home
                ? new BracketSlotSource(HomeSourceType, HomeSourceGroupId, HomeSourcePosition, HomeTournamentTeamId)
                : new BracketSlotSource(AwaySourceType, AwaySourceGroupId, AwaySourcePosition, AwayTournamentTeamId);

        public void SetSlotSource(BracketSlot slot, BracketSlotSource source)
        {
            if (slot == BracketSlot.Home)
            {
                HomeSourceType = source.SourceType;
                HomeSourceGroupId = source.GroupId;
                HomeSourcePosition = source.Position;
                HomeTournamentTeamId = source.TournamentTeamId;
            }
            else
            {
                AwaySourceType = source.SourceType;
                AwaySourceGroupId = source.GroupId;
                AwaySourcePosition = source.Position;
                AwayTournamentTeamId = source.TournamentTeamId;
            }

            SetUpdatedAt();
        }

        // Slot kolejnej rundy wypełniony awansem z wolnego losu, który po zamianie par już nie obowiązuje.
        public void ClearSlot(BracketSlot slot)
        {
            if (slot == BracketSlot.Home)
                HomeTournamentTeamId = null;
            else
                AwayTournamentTeamId = null;

            SetUpdatedAt();
        }

        public void ClearMatch()
        {
            MatchId = null;
            SetUpdatedAt();
        }

        public void PlanSlot(Guid pitchId, DateTimeOffset scheduledAt)
        {
            PlannedPitchId = pitchId;
            PlannedAt = scheduledAt;
            SetUpdatedAt();
        }

        public void ClearPlannedSlot()
        {
            PlannedPitchId = null;
            PlannedAt = null;
            PlannedRefereeId = null;
            SetUpdatedAt();
        }

        public void PlanReferee(Guid? refereeId)
        {
            PlannedRefereeId = refereeId;
            SetUpdatedAt();
        }

        public Result AssignMatch(Guid matchId)
        {
            if (!IsFullyResolved)
                return Result.Failure(Error.Conflict("IsFullyResolved", "Cannot assign a match before both slots are resolved"));

            if (MatchId.HasValue)
                return Result.Failure(Error.Conflict("MatchId", "This node already has a match assigned"));

            MatchId = matchId;
            SetUpdatedAt();

            return Result.Success();
        }
    }
}
