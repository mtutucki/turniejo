using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Matches.Application.Matches.AddAssist;
using Turniejo.Matches.Application.Matches.AddCard;
using Turniejo.Matches.Application.Matches.AddGoal;
using Turniejo.Matches.Application.Matches.AssignMatchPitch;
using Turniejo.Matches.Application.Matches.AssignPlannedMatchReferee;
using Turniejo.Matches.Application.Matches.AssignRefereeToMatch;
using Turniejo.Matches.Application.Matches.ClaimMatch;
using Turniejo.Matches.Application.Matches.ChangeFinishedMatchResult;
using Turniejo.Matches.Application.Matches.RecordMatchResult;
using Turniejo.Matches.Application.Matches.CorrectFinishedMatchScore;
using Turniejo.Matches.Application.Matches.FinishMatch;
using Turniejo.Matches.Application.Matches.GetAssignedMatches;
using Turniejo.Matches.Application.Matches.GetMatchLocation;
using Turniejo.Matches.Application.Matches.GetMatchById;
using Turniejo.Matches.Application.Matches.GetMatchClock;
using Turniejo.Matches.Application.Matches.GetMatchEvents;
using Turniejo.Matches.Application.Matches.GetMatchLivePanel;
using Turniejo.Matches.Application.Matches.GetMatchScore;
using Turniejo.Matches.Application.Matches.GetMatchSummary;
using Turniejo.Matches.Application.Matches.GetMatchTimeline;
using Turniejo.Matches.Application.Matches.GetOpenMatches;
using Turniejo.Matches.Application.Matches.PauseMatch;
using Turniejo.Matches.Application.Matches.ReleaseMatch;
using Turniejo.Matches.Application.Matches.RescheduleMatch;
using Turniejo.Matches.Application.Matches.ResumeMatch;
using Turniejo.Matches.Application.Matches.RevertMatchEvent;
using Turniejo.Matches.Application.Matches.SelectMatchMvp;
using Turniejo.Matches.Application.Matches.SetMatchScore;
using Turniejo.Matches.Application.Matches.SetGoalPlayers;
using Turniejo.Matches.Application.Matches.StartMatch;
using Turniejo.Matches.Application.Matches.UpdateMatchEvent;
using Turniejo.Matches.Application.Matches.UpdatePlannedMatchSchedule;
using Turniejo.Matches.Application.Matches.UpdateMatchSchedule;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Application.Pitches.AddPitch;
using Turniejo.Matches.Application.Pitches.DeletePitch;
using Turniejo.Matches.Application.Pitches.GetTournamentPitches;
using Turniejo.Matches.Application.Pitches.UpdatePitch;
using Turniejo.Matches.Application.Schedule.ClearSchedule;
using Turniejo.Matches.Application.Schedule.GenerateSchedule;
using Turniejo.Matches.Application.Schedule.GetSchedule;

namespace Turniejo.Api.Controllers
{
    [Route("api/tournaments/{tournamentId}")]
    [ApiController]
    public sealed class MatchesController(ISender sender) : ControllerBase
    {
        [HttpPost("pitches")]
        public async Task<IActionResult> AddPitch([FromRoute] Guid tournamentId, [FromBody] AddPitchRequest request, CancellationToken cancellationToken)
        {
            var command = new AddPitchCommand(tournamentId, request.Name, request.Description, request.GroupIds);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/pitches/{result.Value.PitchId}", result.Value);
        }

        [HttpPut("pitches/{pitchId}")]
        public async Task<IActionResult> UpdatePitch([FromRoute] Guid tournamentId, [FromRoute] Guid pitchId, [FromBody] UpdatePitchRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdatePitchCommand(tournamentId, pitchId, request.Name, request.Description, request.IsActive, request.GroupIds);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("pitches/{pitchId}")]
        public async Task<IActionResult> DeletePitch([FromRoute] Guid tournamentId, [FromRoute] Guid pitchId, CancellationToken cancellationToken)
        {
            var command = new DeletePitchCommand(tournamentId, pitchId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("pitches")]
        public async Task<IActionResult> GetTournamentPitches([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var query = new GetTournamentPitchesQuery(tournamentId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("schedule/generate")]
        public async Task<IActionResult> GenerateSchedule([FromRoute] Guid tournamentId, [FromBody] GenerateScheduleRequest request, CancellationToken cancellationToken)
        {
            var command = new GenerateScheduleCommand(
                tournamentId, request.StartAt, request.EndAt, request.PitchIds,
                request.Windows?.Select(w => new ScheduleWindow(w.StartAt, w.EndAt, w.MaxMatches)).ToList());
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("schedule")]
        public async Task<IActionResult> ClearSchedule([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ClearScheduleCommand(tournamentId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetSchedule([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var query = new GetScheduleQuery(tournamentId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{matchId}")]
        public async Task<IActionResult> GetMatchById([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var query = new GetMatchByIdQuery(tournamentId, matchId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("matches/{matchId}/schedule")]
        public async Task<IActionResult> UpdateMatchSchedule([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] UpdateMatchScheduleRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateMatchScheduleCommand(tournamentId, matchId, request.PitchId, request.ScheduledAt, request.ShiftFollowing, request.ShiftOtherPitches);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("matches/{matchId}/pitch")]
        public async Task<IActionResult> AssignMatchPitch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] AssignMatchPitchRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignMatchPitchCommand(tournamentId, matchId, request.PitchId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("matches/{matchId}/time")]
        public async Task<IActionResult> RescheduleMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] RescheduleMatchRequest request, CancellationToken cancellationToken)
        {
            var command = new RescheduleMatchCommand(tournamentId, matchId, request.ScheduledAt);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("matches/{matchId}/referee")]
        public async Task<IActionResult> AssignRefereeToMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] AssignRefereeToMatchRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignRefereeToMatchCommand(tournamentId, matchId, request.RefereeId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        // Mecz pucharu, który jeszcze nie powstał (czeka na zwycięzców), to węzeł drabinki z planowanym terminem.
        [HttpPut("bracket-nodes/{nodeId}/schedule")]
        public async Task<IActionResult> UpdatePlannedMatchSchedule([FromRoute] Guid tournamentId, [FromRoute] Guid nodeId, [FromBody] UpdatePlannedMatchScheduleRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdatePlannedMatchScheduleCommand(tournamentId, nodeId, request.PitchId, request.ScheduledAt);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("bracket-nodes/{nodeId}/referee")]
        public async Task<IActionResult> AssignPlannedMatchReferee([FromRoute] Guid tournamentId, [FromRoute] Guid nodeId, [FromBody] AssignRefereeToMatchRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignPlannedMatchRefereeCommand(tournamentId, nodeId, request.RefereeId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/referee/claim")]
        public async Task<IActionResult> ClaimMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ClaimMatchCommand(tournamentId, matchId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/referee/release")]
        public async Task<IActionResult> ReleaseMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ReleaseMatchCommand(tournamentId, matchId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("/api/referee/matches/open")]
        public async Task<IActionResult> GetOpenMatches(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetOpenMatchesQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("/api/referee/matches")]
        public async Task<IActionResult> GetAssignedMatches(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetAssignedMatchesQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("/api/matches/{id}/location")]
        public async Task<IActionResult> GetMatchLocation([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMatchLocationQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/start")]
        public async Task<IActionResult> StartMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var command = new StartMatchCommand(tournamentId, matchId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/pause")]
        public async Task<IActionResult> PauseMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var command = new PauseMatchCommand(tournamentId, matchId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/resume")]
        public async Task<IActionResult> ResumeMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var command = new ResumeMatchCommand(tournamentId, matchId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/finish")]
        public async Task<IActionResult> FinishMatch([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] FinishMatchRequest request, CancellationToken cancellationToken)
        {
            var command = new FinishMatchCommand(
                tournamentId, matchId, request.ResultType, request.WinnerTournamentTeamId,
                request.HomePenaltyScore, request.AwayPenaltyScore);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{matchId}/live-panel")]
        public async Task<IActionResult> GetMatchLivePanel([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var query = new GetMatchLivePanelQuery(tournamentId, matchId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{matchId}/clock")]
        public async Task<IActionResult> GetMatchClock([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var query = new GetMatchClockQuery(tournamentId, matchId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{matchId}/summary")]
        public async Task<IActionResult> GetMatchSummary([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var query = new GetMatchSummaryQuery(tournamentId, matchId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/goals")]
        public async Task<IActionResult> AddGoal([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] AddGoalRequest request, CancellationToken cancellationToken)
        {
            var command = new AddGoalCommand(
                tournamentId, matchId, request.TournamentTeamId, request.PlayerId, request.AssistPlayerId,
                request.GoalType, request.Minute, request.Second);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/cards")]
        public async Task<IActionResult> AddCard([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] AddCardRequest request, CancellationToken cancellationToken)
        {
            var command = new AddCardCommand(
                tournamentId, matchId, request.TournamentTeamId, request.PlayerId, request.CardType, request.Minute, request.Second);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/assists")]
        public async Task<IActionResult> AddAssist([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] AddAssistRequest request, CancellationToken cancellationToken)
        {
            var command = new AddAssistCommand(
                tournamentId, matchId, request.TournamentTeamId, request.PlayerId, request.ScorerPlayerId, request.Minute, request.Second);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matches/{matchId}/mvp")]
        public async Task<IActionResult> SelectMatchMvp([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] SelectMatchMvpRequest request, CancellationToken cancellationToken)
        {
            var command = new SelectMatchMvpCommand(tournamentId, matchId, request.TournamentTeamId, request.PlayerId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{matchId}/events")]
        public async Task<IActionResult> GetMatchEvents([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var query = new GetMatchEventsQuery(tournamentId, matchId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{matchId}/timeline")]
        public async Task<IActionResult> GetMatchTimeline([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var query = new GetMatchTimelineQuery(tournamentId, matchId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{matchId}/score")]
        public async Task<IActionResult> GetMatchScore([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, CancellationToken cancellationToken)
        {
            var query = new GetMatchScoreQuery(tournamentId, matchId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("matches/{matchId}/events/{eventId}")]
        public async Task<IActionResult> RevertMatchEvent([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromRoute] Guid eventId, CancellationToken cancellationToken)
        {
            var command = new RevertMatchEventCommand(tournamentId, matchId, eventId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("matches/{matchId}/events/{eventId}")]
        public async Task<IActionResult> UpdateMatchEvent([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromRoute] Guid eventId, [FromBody] UpdateMatchEventRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateMatchEventCommand(
                tournamentId, matchId, eventId, request.Type, request.Minute, request.Second, request.PlayerId, request.RelatedPlayerId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("matches/{matchId}/events/{eventId}/players")]
        public async Task<IActionResult> SetGoalPlayers([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromRoute] Guid eventId, [FromBody] SetGoalPlayersRequest request, CancellationToken cancellationToken)
        {
            var command = new SetGoalPlayersCommand(tournamentId, matchId, eventId, request.PlayerId, request.AssistPlayerId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("matches/{matchId}/score")]
        public async Task<IActionResult> SetMatchScore([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] SetMatchScoreRequest request, CancellationToken cancellationToken)
        {
            var command = new SetMatchScoreCommand(tournamentId, matchId, request.HomeScore, request.AwayScore);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("matches/{matchId}/correct-score")]
        public async Task<IActionResult> CorrectFinishedMatchScore([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] CorrectFinishedMatchScoreRequest request, CancellationToken cancellationToken)
        {
            var command = new CorrectFinishedMatchScoreCommand(tournamentId, matchId, request.HomeScore, request.AwayScore, request.WinnerTournamentTeamId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        // Wynik wpisany od razu, bez prowadzenia meczu na żywo (mecz musi być jeszcze niezaczęty).
        [HttpPost("matches/{matchId}/record-result")]
        public async Task<IActionResult> RecordMatchResult([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] RecordMatchResultRequest request, CancellationToken cancellationToken)
        {
            var command = new RecordMatchResultCommand(
                tournamentId, matchId, request.HomeScore, request.AwayScore, request.ResultType, request.WinnerTournamentTeamId,
                request.HomePenaltyScore, request.AwayPenaltyScore);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        // Zmiana rozstrzygnięcia zakończonego meczu (walkower, karne). Sędzia w oknie poprawek, organizator zawsze.
        [HttpPatch("matches/{matchId}/result")]
        public async Task<IActionResult> ChangeFinishedMatchResult([FromRoute] Guid tournamentId, [FromRoute] Guid matchId, [FromBody] ChangeMatchResultRequest request, CancellationToken cancellationToken)
        {
            var command = new ChangeFinishedMatchResultCommand(
                tournamentId, matchId, request.ResultType, request.WinnerTournamentTeamId,
                request.HomePenaltyScore, request.AwayPenaltyScore, request.HomeScore, request.AwayScore);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record AddPitchRequest(string Name, string? Description, IReadOnlyList<Guid>? GroupIds = null);
        public sealed record UpdatePitchRequest(string Name, string? Description, bool IsActive, IReadOnlyList<Guid>? GroupIds = null);
        public sealed record ScheduleWindowRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, int? MaxMatches = null);
        public sealed record GenerateScheduleRequest(
            DateTimeOffset StartAt,
            DateTimeOffset EndAt,
            IReadOnlyList<Guid> PitchIds,
            IReadOnlyList<ScheduleWindowRequest>? Windows = null);
        public sealed record UpdateMatchScheduleRequest(Guid? PitchId, DateTimeOffset ScheduledAt, bool ShiftFollowing = false, bool ShiftOtherPitches = false);

        public sealed record UpdatePlannedMatchScheduleRequest(Guid PitchId, DateTimeOffset ScheduledAt);
        public sealed record AssignMatchPitchRequest(Guid? PitchId);
        public sealed record RescheduleMatchRequest(DateTimeOffset ScheduledAt);
        public sealed record AssignRefereeToMatchRequest(Guid? RefereeId);
        public sealed record FinishMatchRequest(
            MatchResultType ResultType, Guid? WinnerTournamentTeamId, int? HomePenaltyScore = null, int? AwayPenaltyScore = null);
        public sealed record AddGoalRequest(Guid TournamentTeamId, Guid? PlayerId, Guid? AssistPlayerId, MatchEventType GoalType, int Minute, int? Second);
        public sealed record AddCardRequest(Guid TournamentTeamId, Guid PlayerId, MatchEventType CardType, int Minute, int? Second);
        public sealed record AddAssistRequest(Guid TournamentTeamId, Guid PlayerId, Guid? ScorerPlayerId, int Minute, int? Second);
        public sealed record SelectMatchMvpRequest(Guid TournamentTeamId, Guid PlayerId);
        public sealed record UpdateMatchEventRequest(MatchEventType Type, int Minute, int? Second, Guid? PlayerId, Guid? RelatedPlayerId);
        public sealed record SetMatchScoreRequest(int HomeScore, int AwayScore);
        public sealed record SetGoalPlayersRequest(Guid? PlayerId, Guid? AssistPlayerId);
        public sealed record ChangeMatchResultRequest(
            MatchResultType ResultType, Guid? WinnerTournamentTeamId,
            int? HomePenaltyScore = null, int? AwayPenaltyScore = null, int? HomeScore = null, int? AwayScore = null);
        public sealed record CorrectFinishedMatchScoreRequest(int HomeScore, int AwayScore, Guid? WinnerTournamentTeamId);
        public sealed record RecordMatchResultRequest(
            int HomeScore, int AwayScore, MatchResultType ResultType, Guid? WinnerTournamentTeamId,
            int? HomePenaltyScore = null, int? AwayPenaltyScore = null);
    }
}
