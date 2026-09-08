using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Standings.Domain.Entities
{
    public sealed class Standing : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public Guid CategoryId { get; private set; }
        public Guid GroupId { get; private set; }
        public Guid TournamentTeamId { get; private set; }
        public int MatchesPlayed { get; private set; }
        public int Wins { get; private set; }
        public int Draws { get; private set; }
        public int Losses { get; private set; }
        public int GoalsFor { get; private set; }
        public int GoalsAgainst { get; private set; }
        public int GoalDifference { get; private set; }
        public int Points { get; private set; }
        public int Position { get; private set; }

        private Standing() { }
        private Standing(Guid id) : base(id) { }

        public static Result<Standing> Create(Guid id, Guid tournamentId, Guid categoryId, Guid groupId, Guid tournamentTeamId)
        {
            if (id == Guid.Empty)
                return Result.Failure<Standing>(Error.Validation("Id", "Standing id cannot be empty"));

            if (tournamentId == Guid.Empty)
                return Result.Failure<Standing>(Error.Validation("TournamentId", "Standing tournamentId cannot be empty"));

            if (groupId == Guid.Empty)
                return Result.Failure<Standing>(Error.Validation("GroupId", "Standing groupId cannot be empty"));

            if (tournamentTeamId == Guid.Empty)
                return Result.Failure<Standing>(Error.Validation("TournamentTeamId", "Standing tournamentTeamId cannot be empty"));

            var standing = new Standing(id)
            {
                TournamentId = tournamentId,
                CategoryId = categoryId,
                GroupId = groupId,
                TournamentTeamId = tournamentTeamId
            };

            return Result.Success(standing);
        }

        public Result UpdateStats(int matchesPlayed, int wins, int draws, int losses, int goalsFor, int goalsAgainst, int points)
        {
            if (matchesPlayed < 0 || wins < 0 || draws < 0 || losses < 0 || goalsFor < 0 || goalsAgainst < 0 || points < 0)
                return Result.Failure(Error.Validation("Stats", "Standing stats cannot be negative"));

            MatchesPlayed = matchesPlayed;
            Wins = wins;
            Draws = draws;
            Losses = losses;
            GoalsFor = goalsFor;
            GoalsAgainst = goalsAgainst;
            GoalDifference = goalsFor - goalsAgainst;
            Points = points;
            SetUpdatedAt();

            return Result.Success();
        }

        public void SetPosition(int position)
        {
            Position = position;
            SetUpdatedAt();
        }
    }
}
