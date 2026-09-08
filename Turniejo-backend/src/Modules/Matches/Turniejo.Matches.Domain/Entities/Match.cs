using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Domain.Entities
{
    public sealed class Match : BaseEntity
    {
        public Guid TournamentId { get; private set; }
        public Guid? CategoryId { get; private set; }
        public Guid? GroupId { get; private set; }
        public Guid? BracketNodeId { get; private set; }
        public Guid? PitchId { get; private set; }
        public Guid HomeTournamentTeamId { get; private set; }
        public Guid AwayTournamentTeamId { get; private set; }
        public Guid? RefereeId { get; private set; }
        public DateTimeOffset ScheduledAt { get; private set; }
        public MatchStatus Status { get; private set; } = MatchStatus.Scheduled;
        public StageType StageType { get; private set; }
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public int? HomePenaltyScore { get; private set; }
        public int? AwayPenaltyScore { get; private set; }
        public MatchResultType ResultType { get; private set; } = MatchResultType.NotPlayed;
        public Guid? WinnerTournamentTeamId { get; private set; }
        public DateTimeOffset? StartedAt { get; private set; }
        public DateTimeOffset? FinishedAt { get; private set; }
        public DateTimeOffset? PausedAt { get; private set; }
        public int TotalPausedSeconds { get; private set; }
        public DateTimeOffset? EditLockedAt { get; private set; }
        public uint Version { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private Match() { }
        private Match(Guid id) : base(id) { }

        public static Result<Match> Create(
            Guid id, Guid tournamentId, Guid? categoryId, Guid? groupId,
            Guid homeTournamentTeamId, Guid awayTournamentTeamId, Guid? pitchId,
            DateTimeOffset scheduledAt, StageType stageType)
        {
            if (id == Guid.Empty)
                return Result.Failure<Match>(Error.Validation("Id", "Match id cannot be empty"));

            if (tournamentId == Guid.Empty)
                return Result.Failure<Match>(Error.Validation("TournamentId", "Match tournamentId cannot be empty"));

            if (homeTournamentTeamId == Guid.Empty)
                return Result.Failure<Match>(Error.Validation("HomeTournamentTeamId", "Match homeTournamentTeamId cannot be empty"));

            if (awayTournamentTeamId == Guid.Empty)
                return Result.Failure<Match>(Error.Validation("AwayTournamentTeamId", "Match awayTournamentTeamId cannot be empty"));

            if (homeTournamentTeamId == awayTournamentTeamId)
                return Result.Failure<Match>(Error.Validation("AwayTournamentTeamId", "A team cannot play against itself"));

            var match = new Match(id)
            {
                TournamentId = tournamentId,
                CategoryId = categoryId,
                GroupId = groupId,
                HomeTournamentTeamId = homeTournamentTeamId,
                AwayTournamentTeamId = awayTournamentTeamId,
                PitchId = pitchId,
                ScheduledAt = scheduledAt,
                StageType = stageType
            };

            return Result.Success(match);
        }

        public Result UpdateSchedule(Guid? pitchId, DateTimeOffset scheduledAt, Guid changedByUserId)
        {
            PitchId = pitchId;
            ScheduledAt = scheduledAt;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchScheduleChanged(Id, TournamentId, pitchId, scheduledAt, changedByUserId));

            return Result.Success();
        }

        public Result AssignPitch(Guid? pitchId, Guid changedByUserId)
        {
            PitchId = pitchId;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchPitchChanged(Id, TournamentId, pitchId, changedByUserId));

            return Result.Success();
        }

        public Result Reschedule(DateTimeOffset scheduledAt, Guid changedByUserId)
        {
            ScheduledAt = scheduledAt;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchTimeChanged(Id, TournamentId, scheduledAt, changedByUserId));

            return Result.Success();
        }

        public Result AssignReferee(Guid? refereeId, Guid changedByUserId)
        {
            if (Status is MatchStatus.Live or MatchStatus.Finished)
                return Result.Failure(Error.Conflict("Status", "Cannot change the referee once the match is live or finished"));

            RefereeId = refereeId;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new RefereeAssignedToMatch(Id, TournamentId, refereeId, changedByUserId));

            return Result.Success();
        }

        public Result Start(Guid changedByUserId)
        {
            if (Status != MatchStatus.Scheduled)
                return Result.Failure(Error.Conflict("Status", "Match can only be started from Scheduled status"));

            Status = MatchStatus.Live;
            StartedAt = DateTimeOffset.UtcNow;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchStarted(Id, TournamentId, StartedAt.Value, changedByUserId));

            return Result.Success();
        }

        public Result Pause(Guid changedByUserId)
        {
            if (Status != MatchStatus.Live)
                return Result.Failure(Error.Conflict("Status", "Match can only be paused while Live"));

            Status = MatchStatus.Paused;
            PausedAt = DateTimeOffset.UtcNow;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchPaused(Id, TournamentId, PausedAt.Value, changedByUserId));

            return Result.Success();
        }

        public Result Resume(Guid changedByUserId)
        {
            if (Status != MatchStatus.Paused || PausedAt is null)
                return Result.Failure(Error.Conflict("Status", "Match can only be resumed from Paused status"));

            TotalPausedSeconds += (int)(DateTimeOffset.UtcNow - PausedAt.Value).TotalSeconds;
            PausedAt = null;
            Status = MatchStatus.Live;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchResumed(Id, TournamentId, changedByUserId));

            return Result.Success();
        }

        public Result Finish(
            MatchResultType resultType, Guid? winnerTournamentTeamId, int refereeEditWindowMinutes, Guid changedByUserId,
            int? walkoverWinnerScore = null, int? walkoverLoserScore = null)
        {
            if (Status is not (MatchStatus.Live or MatchStatus.Paused))
                return Result.Failure(Error.Conflict("Status", "Match can only be finished from Live or Paused status"));

            if (resultType == MatchResultType.NotPlayed)
                return Result.Failure(Error.Validation("ResultType", "ResultType must indicate how the match was decided"));

            if (winnerTournamentTeamId.HasValue &&
                winnerTournamentTeamId.Value != HomeTournamentTeamId &&
                winnerTournamentTeamId.Value != AwayTournamentTeamId)
                return Result.Failure(Error.Validation("WinnerTournamentTeamId", "Winner must be one of the two teams playing"));

            if (resultType == MatchResultType.Walkover)
            {
                if (!winnerTournamentTeamId.HasValue)
                    return Result.Failure(Error.Validation("WinnerTournamentTeamId", "Walkover requires a winner"));

                if (!walkoverWinnerScore.HasValue || !walkoverLoserScore.HasValue)
                    return Result.Failure(Error.Validation("ResultType", "Walkover requires the tournament's walkover scores"));

                // WalkoverHomeScore/WalkoverAwayScore w ustawieniach turnieju to wynik zwycięzcy/przegranego
                // walkoweru, nie dosłownie gospodarza/gościa — drużyna, która nie stawiła się na mecz, może
                // równie dobrze być stroną "domową" w terminarzu. Bez tego HomeScore/AwayScore zostawałoby
                // 0:0 i tabela liczyłaby walkower jako remis, ignorując WinnerTournamentTeamId.
                if (winnerTournamentTeamId.Value == HomeTournamentTeamId)
                {
                    HomeScore = walkoverWinnerScore.Value;
                    AwayScore = walkoverLoserScore.Value;
                }
                else
                {
                    HomeScore = walkoverLoserScore.Value;
                    AwayScore = walkoverWinnerScore.Value;
                }
            }

            Status = MatchStatus.Finished;
            FinishedAt = DateTimeOffset.UtcNow;
            ResultType = resultType;
            WinnerTournamentTeamId = winnerTournamentTeamId;
            EditLockedAt = FinishedAt.Value.AddMinutes(refereeEditWindowMinutes);
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchFinished(Id, TournamentId, GroupId, resultType.ToString(), winnerTournamentTeamId, FinishedAt.Value, changedByUserId, HomeScore, AwayScore));

            return Result.Success();
        }

        public int GetElapsedSeconds(DateTimeOffset at)
        {
            if (!StartedAt.HasValue)
                return 0;

            return Math.Max(0, (int)(at - StartedAt.Value).TotalSeconds - TotalPausedSeconds);
        }

        public Result EnsureEditable()
        {
            if (Status is MatchStatus.Live or MatchStatus.Paused)
                return Result.Success();

            if (Status == MatchStatus.Finished && EditLockedAt.HasValue && DateTimeOffset.UtcNow <= EditLockedAt.Value)
                return Result.Success();

            return Result.Failure(Error.Conflict("Status", "Match events can only be recorded while the match is live, paused, or within the post-match edit window"));
        }

        public Result UpdateScore(int homeScore, int awayScore)
        {
            if (homeScore < 0 || awayScore < 0)
                return Result.Failure(Error.Validation("Score", "Score cannot be negative"));

            if (HomeScore == homeScore && AwayScore == awayScore)
                return Result.Success();

            HomeScore = homeScore;
            AwayScore = awayScore;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchScoreChanged(Id, TournamentId, GroupId, homeScore, awayScore));

            return Result.Success();
        }

        public Result SetManualScore(int homeScore, int awayScore, Guid changedByUserId)
        {
            if (Status == MatchStatus.Finished)
                return Result.Failure(Error.Conflict("Status", "Use CorrectFinishedMatchScoreCommand to change the score of a finished match"));

            if (homeScore < 0 || awayScore < 0)
                return Result.Failure(Error.Validation("Score", "Score cannot be negative"));

            HomeScore = homeScore;
            AwayScore = awayScore;
            ResultType = MatchResultType.ManualDecision;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchScoreChanged(Id, TournamentId, GroupId, homeScore, awayScore));

            return Result.Success();
        }

        public Result CorrectFinishedScore(int homeScore, int awayScore, Guid changedByUserId)
        {
            if (Status != MatchStatus.Finished)
                return Result.Failure(Error.Conflict("Status", "Match must be finished to correct its score"));

            if (EditLockedAt.HasValue && DateTimeOffset.UtcNow > EditLockedAt.Value)
                return Result.Failure(Error.Conflict("EditLockedAt", "The post-match edit window has closed"));

            if (homeScore < 0 || awayScore < 0)
                return Result.Failure(Error.Validation("Score", "Score cannot be negative"));

            HomeScore = homeScore;
            AwayScore = awayScore;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchScoreCorrected(Id, TournamentId, GroupId, homeScore, awayScore, changedByUserId));

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
