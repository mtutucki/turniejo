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

        // Termin i boisko zmieniamy tylko przed rozpoczęciem meczu.
        private bool IsSchedulable => Status is MatchStatus.Scheduled or MatchStatus.Ready or MatchStatus.Postponed;

        // Zamiana par w drabince przed startem: mecz zostaje (termin, boisko, sędzia), zmieniają się
        // tylko drużyny. Po rozpoczęciu drużyny są już nie do ruszenia — mecz ma zdarzenia i wynik.
        public Result ChangeTeams(Guid homeTournamentTeamId, Guid awayTournamentTeamId)
        {
            if (!IsSchedulable)
                return Result.Failure(Error.Conflict("Status", "Cannot change the teams once the match has started, finished or been cancelled"));

            if (homeTournamentTeamId == Guid.Empty)
                return Result.Failure(Error.Validation("HomeTournamentTeamId", "Match homeTournamentTeamId cannot be empty"));

            if (awayTournamentTeamId == Guid.Empty)
                return Result.Failure(Error.Validation("AwayTournamentTeamId", "Match awayTournamentTeamId cannot be empty"));

            if (homeTournamentTeamId == awayTournamentTeamId)
                return Result.Failure(Error.Validation("AwayTournamentTeamId", "A team cannot play against itself"));

            HomeTournamentTeamId = homeTournamentTeamId;
            AwayTournamentTeamId = awayTournamentTeamId;
            Version++;
            SetUpdatedAt();

            return Result.Success();
        }

        public Result UpdateSchedule(Guid? pitchId, DateTimeOffset scheduledAt, Guid changedByUserId)
        {
            if (!IsSchedulable)
                return Result.Failure(Error.Conflict("Status", "Cannot change the schedule once the match has started, finished or been cancelled"));

            PitchId = pitchId;
            ScheduledAt = scheduledAt;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchScheduleChanged(Id, TournamentId, pitchId, scheduledAt, changedByUserId));

            return Result.Success();
        }

        public Result AssignPitch(Guid? pitchId, Guid changedByUserId)
        {
            if (!IsSchedulable)
                return Result.Failure(Error.Conflict("Status", "Cannot change the schedule once the match has started, finished or been cancelled"));

            PitchId = pitchId;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchPitchChanged(Id, TournamentId, pitchId, changedByUserId));

            return Result.Success();
        }

        public Result Reschedule(DateTimeOffset scheduledAt, Guid changedByUserId)
        {
            if (!IsSchedulable)
                return Result.Failure(Error.Conflict("Status", "Cannot change the schedule once the match has started, finished or been cancelled"));

            ScheduledAt = scheduledAt;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchTimeChanged(Id, TournamentId, scheduledAt, changedByUserId));

            return Result.Success();
        }

        // Sędziego — inaczej niż termin i boisko — da się zmienić także w trakcie meczu: prowadzący może
        // nagle wypaść i ktoś musi dokończyć mecz. Po zakończeniu/odwołaniu nie ma już czego prowadzić.
        private bool IsOver => Status is MatchStatus.Finished or MatchStatus.Cancelled;

        public Result AssignReferee(Guid? refereeId, Guid changedByUserId)
        {
            if (IsOver)
                return Result.Failure(Error.Conflict("Status", "Cannot change the referee once the match has finished or been cancelled"));

            RefereeId = refereeId;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new RefereeAssignedToMatch(Id, TournamentId, refereeId, changedByUserId));

            return Result.Success();
        }

        // Mecz bez sędziego jest w puli — bierze go pierwszy sędzia turnieju, który się zgłosi.
        public Result ClaimReferee(Guid refereeId)
        {
            if (IsOver)
                return Result.Failure(Error.Conflict("Status", "Cannot change the referee once the match has finished or been cancelled"));

            if (RefereeId.HasValue)
                return Result.Failure(Error.Conflict("RefereeId", "Match already has a referee"));

            RefereeId = refereeId;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new RefereeAssignedToMatch(Id, TournamentId, refereeId, refereeId));

            return Result.Success();
        }

        // Sędzia oddaje mecz z powrotem do puli — także w trakcie, gdy musi nagle zejść z boiska.
        public Result ReleaseReferee(Guid refereeId)
        {
            if (IsOver)
                return Result.Failure(Error.Conflict("Status", "Cannot change the referee once the match has finished or been cancelled"));

            if (RefereeId != refereeId)
                return Result.Failure(Error.Conflict("RefereeId", "You are not the referee of this match"));

            RefereeId = null;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new RefereeAssignedToMatch(Id, TournamentId, null, refereeId));

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
            int? walkoverWinnerScore = null, int? walkoverLoserScore = null,
            int? homePenaltyScore = null, int? awayPenaltyScore = null)
        {
            if (Status is not (MatchStatus.Live or MatchStatus.Paused))
                return Result.Failure(Error.Conflict("Status", "Match can only be finished from Live or Paused status"));

            if (resultType == MatchResultType.NotPlayed)
                return Result.Failure(Error.Validation("ResultType", "ResultType must indicate how the match was decided"));

            if (resultType == MatchResultType.Penalties)
            {
                // Karne rozstrzygają tylko remis; zwycięzca wynika z karnych (wyliczany, jeśli nie podano).
                if (HomeScore != AwayScore)
                    return Result.Failure(Error.Validation("ResultType", "Penalties can only decide a drawn match"));
                if (!homePenaltyScore.HasValue || !awayPenaltyScore.HasValue || homePenaltyScore < 0 || awayPenaltyScore < 0)
                    return Result.Failure(Error.Validation("HomePenaltyScore", "Penalty shootout score is required"));
                if (homePenaltyScore == awayPenaltyScore)
                    return Result.Failure(Error.Validation("HomePenaltyScore", "Penalty shootout cannot end in a draw"));

                var penaltyWinner = homePenaltyScore > awayPenaltyScore ? HomeTournamentTeamId : AwayTournamentTeamId;
                if (winnerTournamentTeamId.HasValue && winnerTournamentTeamId != penaltyWinner)
                    return Result.Failure(Error.Validation("WinnerTournamentTeamId", "Winner must be the team that won the penalty shootout"));

                winnerTournamentTeamId = penaltyWinner;
                HomePenaltyScore = homePenaltyScore;
                AwayPenaltyScore = awayPenaltyScore;
            }

            // Mecz pucharowy musi mieć zwycięzcę (awans w drabince), a w zwykłym czasie i dogrywce
            // zwycięzcą jest drużyna z większą liczbą goli — remis rozstrzygają karne.
            if (StageType != StageType.Group && resultType is MatchResultType.Regular or MatchResultType.ExtraTime)
            {
                if (HomeScore == AwayScore)
                    return Result.Failure(Error.Validation("ResultType", "A knockout match cannot end in a draw — decide it by penalties"));

                var scoreWinner = HomeScore > AwayScore ? HomeTournamentTeamId : AwayTournamentTeamId;
                if (winnerTournamentTeamId.HasValue && winnerTournamentTeamId != scoreWinner)
                    return Result.Failure(Error.Validation("WinnerTournamentTeamId", "Winner must be the team with more goals"));

                winnerTournamentTeamId = scoreWinner;
            }

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

        // Walkower bez rozegrania meczu: rywal wycofał się z turnieju. Finish() wymaga Live/Paused, a ten
        // mecz nigdy się nie zacznie — w pucharze samo odwołanie zostawiało parę martwą (nikt nie awansował).
        // Bez okna na korektę wyniku: nie ma czego poprawiać.
        public Result AwardWalkover(Guid winnerTournamentTeamId, int winnerScore, int loserScore, Guid changedByUserId)
        {
            if (!IsSchedulable)
                return Result.Failure(Error.Conflict("Status", "A walkover can only be awarded before the match has started"));

            if (winnerTournamentTeamId != HomeTournamentTeamId && winnerTournamentTeamId != AwayTournamentTeamId)
                return Result.Failure(Error.Validation("WinnerTournamentTeamId", "Winner must be one of the two teams playing"));

            var homeWins = winnerTournamentTeamId == HomeTournamentTeamId;
            HomeScore = homeWins ? winnerScore : loserScore;
            AwayScore = homeWins ? loserScore : winnerScore;
            Status = MatchStatus.Finished;
            FinishedAt = DateTimeOffset.UtcNow;
            ResultType = MatchResultType.Walkover;
            WinnerTournamentTeamId = winnerTournamentTeamId;
            EditLockedAt = FinishedAt;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchFinished(Id, TournamentId, GroupId, ResultType.ToString(), winnerTournamentTeamId, FinishedAt.Value, changedByUserId, HomeScore, AwayScore));

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

            // Ten wynik jest liczony od nowa z eventów meczowych (goli/rzutów karnych), więc jeśli
            // ResultType był ManualDecision (ustawiony przez SetManualScore), przestaje być aktualny —
            // bez tego kolejny AddGoal/RevertMatchEvent nadpisywał wynik, ale etykieta ManualDecision
            // zostawała, mimo że wynik nie jest już ręczną decyzją.
            var resultTypeIsStale = ResultType == MatchResultType.ManualDecision;

            if (HomeScore == homeScore && AwayScore == awayScore && !resultTypeIsStale)
                return Result.Success();

            HomeScore = homeScore;
            AwayScore = awayScore;

            if (resultTypeIsStale)
                ResultType = MatchResultType.Regular;

            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchScoreChanged(Id, TournamentId, GroupId, homeScore, awayScore));

            // Poprawka goli w oknie edycji po meczu pucharowym może odwrócić wynik — wtedy drabinka
            // (słuchająca MatchScoreCorrected) musi przesunąć dalej nowego zwycięzcę.
            if (RefreshKnockoutWinner())
                _domainEvents.Add(new MatchScoreCorrected(Id, TournamentId, GroupId, HomeScore, AwayScore, WinnerTournamentTeamId, Guid.Empty));

            return Result.Success();
        }

        // Zwycięzca meczu pucharowego zapisywał się tylko w Finish, więc późniejsza poprawka goli zmieniała
        // wynik, a dalej w drabince zostawała stara drużyna. Zwraca true, gdy zwycięzca się zmienił.
        // Remis bez karnych (np. w trakcie poprawiania 2:1 na 1:2) zostawia dotychczasowego zwycięzcę.
        public bool RefreshKnockoutWinner()
        {
            if (Status != MatchStatus.Finished || StageType == StageType.Group || ResultType == MatchResultType.Walkover)
                return false;

            Guid winner;
            if (HomeScore != AwayScore)
                winner = HomeScore > AwayScore ? HomeTournamentTeamId : AwayTournamentTeamId;
            else if (ResultType == MatchResultType.Penalties &&
                     HomePenaltyScore is { } homePenalties && AwayPenaltyScore is { } awayPenalties && homePenalties != awayPenalties)
                winner = homePenalties > awayPenalties ? HomeTournamentTeamId : AwayTournamentTeamId;
            else
                return false;

            if (WinnerTournamentTeamId == winner)
                return false;

            WinnerTournamentTeamId = winner;
            Version++;
            SetUpdatedAt();

            return true;
        }

        public Result SetManualScore(int homeScore, int awayScore, Guid changedByUserId)
        {
            if (Status == MatchStatus.Finished)
                return Result.Failure(Error.Conflict("Status", "Use CorrectFinishedMatchScoreCommand to change the score of a finished match"));

            // Blokada obejmowała tylko Finished — dało się ustawić wynik meczowi, który jeszcze się
            // nie zaczął (Scheduled/Ready) albo nigdy się nie odbędzie (Postponed/Cancelled).
            if (Status is not (MatchStatus.Live or MatchStatus.Paused))
                return Result.Failure(Error.Conflict("Status", "Match must be live or paused to set a manual score"));

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

        public Result CorrectFinishedScore(int homeScore, int awayScore, Guid? winnerTournamentTeamId, Guid changedByUserId)
        {
            if (Status != MatchStatus.Finished)
                return Result.Failure(Error.Conflict("Status", "Match must be finished to correct its score"));

            if (EditLockedAt.HasValue && DateTimeOffset.UtcNow > EditLockedAt.Value)
                return Result.Failure(Error.Conflict("EditLockedAt", "The post-match edit window has closed"));

            if (homeScore < 0 || awayScore < 0)
                return Result.Failure(Error.Validation("Score", "Score cannot be negative"));

            // Zwycięzca jest deklarowany przez wywołującego (tak samo jak w Finish) — nie da się go
            // wywnioskować z samego wyniku przy ExtraTime/Penalties. Bez tego korekta zmieniała
            // HomeScore/AwayScore, ale WinnerTournamentTeamId zostawał nieaktualny, więc drabinka
            // (subskrybująca MatchScoreCorrected) nie miała czym się kierować przy ponownym awansie.
            if (winnerTournamentTeamId.HasValue &&
                winnerTournamentTeamId.Value != HomeTournamentTeamId &&
                winnerTournamentTeamId.Value != AwayTournamentTeamId)
                return Result.Failure(Error.Validation("WinnerTournamentTeamId", "Winner must be one of the two teams playing"));

            HomeScore = homeScore;
            AwayScore = awayScore;
            WinnerTournamentTeamId = winnerTournamentTeamId;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchScoreCorrected(Id, TournamentId, GroupId, homeScore, awayScore, winnerTournamentTeamId, changedByUserId));

            return Result.Success();
        }

        public Result Cancel(string reason, Guid changedByUserId)
        {
            if (Status is MatchStatus.Live or MatchStatus.Paused or MatchStatus.Finished)
                return Result.Failure(Error.Conflict("Status", "Cannot cancel a match that is live, paused, or already finished"));

            if (Status == MatchStatus.Cancelled)
                return Result.Failure(Error.Conflict("Status", "Match is already cancelled"));

            Status = MatchStatus.Cancelled;
            Version++;
            SetUpdatedAt();

            _domainEvents.Add(new MatchCancelled(Id, TournamentId, GroupId, reason, changedByUserId));

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
