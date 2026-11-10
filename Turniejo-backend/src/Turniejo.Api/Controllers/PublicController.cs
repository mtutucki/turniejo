using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Brackets.Application.Brackets.GetPublicBracket;
using Turniejo.Matches.Application.Matches.GetPublicLiveMatch;
using Turniejo.Matches.Application.Schedule.GetPublicResults;
using Turniejo.Matches.Application.Schedule.GetPublicSchedule;
using Turniejo.Matches.Application.Stats.GetPublicMvpRanking;
using Turniejo.Matches.Application.Stats.GetPublicTopAssists;
using Turniejo.Matches.Application.Stats.GetPublicTopScorers;
using Turniejo.Players.Application.TournamentPlayers.GetPublicPlayerProfile;
using Turniejo.Players.Application.TournamentPlayers.GetPublicTeamSquad;
using Turniejo.Standings.Application.Standings.GetPublicStandings;
using Turniejo.Teams.Application.TournamentTeams.GetPublicTeamProfile;
using Turniejo.Teams.Application.TournamentTeams.GetPublicTeams;
using Turniejo.Tournaments.Application.Tournaments.GetPublicTournamentPage;
using Turniejo.Tournaments.Application.Tournaments.GetPublicTournaments;

using Turniejo.Brackets.Application.SecondStage.GetPublicSecondStage;

namespace Turniejo.Api.Controllers
{
    [Route("public")]
    [ApiController]
    public sealed class PublicController(ISender sender) : ControllerBase
    {
        [HttpGet("tournaments")]
        public async Task<IActionResult> GetTournaments(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicTournamentsQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}")]
        public async Task<IActionResult> GetTournamentPage([FromRoute] string slug, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicTournamentPageQuery(slug), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/teams")]
        public async Task<IActionResult> GetTeams([FromRoute] string slug, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicTeamsQuery(slug), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/teams/{teamId}")]
        public async Task<IActionResult> GetTeamProfile([FromRoute] string slug, [FromRoute] Guid teamId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicTeamProfileQuery(slug, teamId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/teams/{teamId}/players")]
        public async Task<IActionResult> GetTeamSquad([FromRoute] string slug, [FromRoute] Guid teamId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicTeamSquadQuery(slug, teamId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/schedule")]
        public async Task<IActionResult> GetSchedule([FromRoute] string slug, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicScheduleQuery(slug), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/results")]
        public async Task<IActionResult> GetResults([FromRoute] string slug, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicResultsQuery(slug), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/standings")]
        public async Task<IActionResult> GetStandings([FromRoute] string slug, [FromQuery] Guid groupId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicStandingsQuery(slug, groupId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/bracket")]
        public async Task<IActionResult> GetBracket([FromRoute] string slug, [FromQuery] Guid categoryId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicBracketQuery(slug, categoryId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/second-stage")]
        public async Task<IActionResult> GetSecondStage([FromRoute] string slug, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicSecondStageQuery(slug), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matches/{id}/live")]
        public async Task<IActionResult> GetLiveMatch([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicLiveMatchQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("players/{id}")]
        public async Task<IActionResult> GetPlayerProfile([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPublicPlayerProfileQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/stats/top-scorers")]
        public async Task<IActionResult> GetTopScorers([FromRoute] string slug, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
        {
            var result = await sender.Send(new GetPublicTopScorersQuery(slug, top), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/stats/top-assists")]
        public async Task<IActionResult> GetTopAssists([FromRoute] string slug, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
        {
            var result = await sender.Send(new GetPublicTopAssistsQuery(slug, top), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("tournaments/{slug}/stats/mvp-ranking")]
        public async Task<IActionResult> GetMvpRanking([FromRoute] string slug, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
        {
            var result = await sender.Send(new GetPublicMvpRankingQuery(slug, top), cancellationToken);
            return result.ToActionResult();
        }
    }
}
