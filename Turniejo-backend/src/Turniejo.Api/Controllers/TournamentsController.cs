using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Turniejo.Api.Extensions;
using Turniejo.Players.Application.TournamentPlayers.AssignPlayerToTournamentTeam;
using Turniejo.Players.Application.TournamentPlayers.CreatePlayerInTournamentTeam;
using Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers;
using Turniejo.Players.Application.TournamentPlayers.UpdateTournamentPlayer;
using Turniejo.Players.Domain.Enums;
using Turniejo.Teams.Application.TournamentTeams.AddTeamToTournament;
using Turniejo.Teams.Application.TournamentTeams.AssignCoachToTeam;
using Turniejo.Teams.Application.TournamentTeams.AssignTeamToGroup;
using Turniejo.Teams.Application.TournamentTeams.AssignUserToTournamentTeam;
using Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamPaymentStatus;
using Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamRegistrationStatus;
using Turniejo.Teams.Application.TournamentTeams.GetTeamStaff;
using Turniejo.Teams.Application.TournamentTeams.GetTournamentTeams;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Tournaments.Application.TieBreakerRules.GetTieBreakerRules;
using Turniejo.Tournaments.Application.TieBreakerRules.SetTieBreakerRules;
using Turniejo.Tournaments.Application.TournamentGroups.AddTournamentGroup;
using Turniejo.Tournaments.Application.TournamentGroups.DeleteTournamentGroup;
using Turniejo.Tournaments.Application.TournamentGroups.GetTournamentGroups;
using Turniejo.Tournaments.Application.TournamentGroups.UpdateTournamentGroup;
using Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus;
using Turniejo.Tournaments.Application.Tournaments.DeleteTournament;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentById;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentDashboard;
using Turniejo.Tournaments.Application.Tournaments.GetTournaments;
using Turniejo.Tournaments.Application.Tournaments.JoinTournament;
using Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat;
using Turniejo.Tournaments.Application.Tournaments.TournamentSetting.UpdateTournamentSettings;
using Turniejo.Tournaments.Application.Tournaments.UpdateTournament;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentPublicSettings;
using Turniejo.Tournaments.Application.Tournaments.UpdateTournamentPublicSettings;
using Turniejo.Tournaments.Application.TournamentsCategories.AddTournamentCategory;
using Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory;
using Turniejo.Tournaments.Application.TournamentsCategories.GetTournamentCategories;
using Turniejo.Tournaments.Application.TournamentsCategories.UpdateTournamentCategory;
using Turniejo.Tournaments.Application.TournamentSetting.GetTournamentSettings;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Users.Application.Users.GetTournamentMembers;

namespace Turniejo.Api.Controllers
{
    [Route("api/tournaments")]
    [ApiController]
    public sealed class TournamentsController(ISender sender) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTournamentCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{result.Value.TournamentId}", result.Value);
        }

        [HttpPost("join")]
        [EnableRateLimiting(RateLimitingExtensions.JoinTournament)]
        public async Task<IActionResult> Join([FromBody] JoinTournamentCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetTournamentsByOrganizationId([FromQuery] GetTournamentsQuery query, CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}")]
        public async Task<IActionResult> GetTournamentById([FromRoute] GetTournamentByIdQuery query, CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}/members")]
        public async Task<IActionResult> GetTournamentMembers([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTournamentMembersQuery(tournamentId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}/dashboard")]
        public async Task<IActionResult> GetTournamentDashboard([FromRoute] Guid tournamentId, [FromQuery] int upcomingMatchesCount = 5, CancellationToken cancellationToken = default)
        {
            var query = new GetTournamentDashboardQuery(tournamentId, upcomingMatchesCount);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("{tournamentId}/status")]
        public async Task<IActionResult> ChangeTournamentStatus([FromRoute] Guid tournamentId, [FromBody] ChangeTournamentStatusRequest request, CancellationToken cancellationToken)
        {
            var command = new ChangeTournamentStatusCommand(tournamentId, request.TournamentStatus);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        // Tylko anulowany turniej (miękkie usunięcie — znika z list i portalu).
        [HttpDelete("{tournamentId}")]
        public async Task<IActionResult> DeleteTournament([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new DeleteTournamentCommand(tournamentId), cancellationToken);
            return result.IsFailure ? result.ToActionResult() : NoContent();
        }

        [HttpPatch("{tournamentId}/format")]
        public async Task<IActionResult> UpdateTournamentFormat([FromRoute] Guid tournamentId, [FromBody] UpdateTournamentFormatRequest request, CancellationToken cancellationToken)
        {
            var command = new SetTournamentFormatCommand(tournamentId, request.TournamentFormat);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{tournamentId}")]
        public async Task<IActionResult> UpdateTournamentDetails([FromRoute] Guid tournamentId, [FromBody] UpdateTournamentDetailsRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentDetailsCommand(tournamentId, request.Name, request.StartAt, request.EndAt, request.TeamLimit, request.Description);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{tournamentId}/categories")]
        public async Task<IActionResult> AddTournamentCategory([FromRoute] Guid tournamentId, [FromBody] AddTournamentCategoryRequest request, CancellationToken cancellationToken)
        {
            var command = new AddTournamentCategoryCommand(tournamentId, request.Name, request.MinBirthDate, request.MaxBirthDate, request.Order);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/categories/{result.Value.TournamentCategoryId}", result.Value);
        }

        [HttpPut("{tournamentId}/categories/{categoryId}")]
        public async Task<IActionResult> UpdateTournamentCategory([FromRoute] Guid tournamentId, [FromRoute] Guid categoryId, [FromBody] UpdateTournamentCategoryRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentCategoryCommand(tournamentId, categoryId, request.Name, request.MinBirthDate, request.MaxBirthDate, request.Order);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{tournamentId}/categories/{categoryId}")]
        public async Task<IActionResult> DeleteTournamentCategory([FromRoute] Guid tournamentId, [FromRoute] Guid categoryId, CancellationToken cancellationToken)
        {
            var command = new DeleteTournamentCategoryCommand(tournamentId, categoryId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}/categories")]
        public async Task<IActionResult> GetTournamentCategories([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var query = new GetTournamentCategoriesQuery(tournamentId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{tournamentId}/groups")]
        public async Task<IActionResult> AddTournamentGroup([FromRoute] Guid tournamentId, [FromBody] AddTournamentGroupRequest request, CancellationToken cancellationToken)
        {
            var command = new AddTournamentGroupCommand(tournamentId, request.CategoryId, request.Name, request.Order);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/groups/{result.Value.TournamentGroupId}", result.Value);
        }

        [HttpGet("{tournamentId}/groups")]
        public async Task<IActionResult> GetTournamentGroups([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var query = new GetTournamentGroupsQuery(tournamentId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{tournamentId}/groups/{groupId}")]
        public async Task<IActionResult> UpdateTournamentGroup([FromRoute] Guid tournamentId, [FromRoute] Guid groupId, [FromBody] UpdateTournamentGroupRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentGroupCommand(tournamentId, groupId, request.Name, request.Order);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{tournamentId}/groups/{groupId}")]
        public async Task<IActionResult> DeleteTournamentGroup([FromRoute] Guid tournamentId, [FromRoute] Guid groupId, CancellationToken cancellationToken)
        {
            var command = new DeleteTournamentGroupCommand(tournamentId, groupId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}/tiebreaker-rules")]
        public async Task<IActionResult> GetTieBreakerRules([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var query = new GetTieBreakerRulesQuery(tournamentId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{tournamentId}/tiebreaker-rules")]
        public async Task<IActionResult> SetTieBreakerRules([FromRoute] Guid tournamentId, [FromBody] SetTieBreakerRulesRequest request, CancellationToken cancellationToken)
        {
            var command = new SetTieBreakerRulesCommand(tournamentId, request.Rules);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{tournamentId}/settings")]
        public async Task<IActionResult> UpdateTournamentSettings([FromRoute] Guid tournamentId, [FromBody] UpdateTournamentSettingsRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentSettingsCommand(
                tournamentId,
                request.PointsForWin,
                request.PointsForDraw,
                request.PointsForLoss,
                request.MatchDurationMinutes,
                request.BreakDurationMinutes,
                request.IsThirdPlaceMatchEnabled,
                request.RefereeEditWindowMinutes,
                request.WalkoverHomeScore,
                request.WalkoverAwayScore,
                request.CanCoachEditSquadAfterStart);

            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}/settings")]
        public async Task<IActionResult> GetTournamentSettings([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var query = new GetTournamentSettingsQuery(tournamentId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}/public-settings")]
        public async Task<IActionResult> GetTournamentPublicSettings([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTournamentPublicSettingsQuery(tournamentId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("{tournamentId}/public-settings")]
        public async Task<IActionResult> UpdateTournamentPublicSettings([FromRoute] Guid tournamentId, [FromBody] UpdateTournamentPublicSettingsRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentPublicSettingsCommand(
                tournamentId, request.IsPublic, request.ShowTeams, request.ShowPlayers,
                request.ShowSchedule, request.ShowStandings, request.ShowBracket, request.ShowLiveMatches, request.ShowStats,
                request.AbbreviatePlayerNames);

            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{tournamentId}/teams")]
        public async Task<IActionResult> AddTeamToTournament([FromRoute] Guid tournamentId, [FromBody] AddTeamToTournamentRequest request, CancellationToken cancellationToken)
        {
            var command = new AddTeamToTournamentCommand(tournamentId, request.TeamId, request.CategoryId);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/teams/{result.Value.TournamentTeamId}", result.Value);
        }

        [HttpGet("{tournamentId}/teams")]
        public async Task<IActionResult> GetTournamentTeams([FromRoute] Guid tournamentId, CancellationToken cancellationToken)
        {
            var query = new GetTournamentTeamsQuery(tournamentId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("{tournamentId}/tournament-teams/{tournamentTeamId}/registration-status")]
        public async Task<IActionResult> ChangeTournamentTeamRegistrationStatus([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromBody] ChangeTournamentTeamRegistrationStatusRequest request, CancellationToken cancellationToken)
        {
            var command = new ChangeTournamentTeamRegistrationStatusCommand(tournamentId, tournamentTeamId, request.RegistrationStatus);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("{tournamentId}/tournament-teams/{tournamentTeamId}/payment-status")]
        public async Task<IActionResult> ChangeTournamentTeamPaymentStatus([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromBody] ChangeTournamentTeamPaymentStatusRequest request, CancellationToken cancellationToken)
        {
            var command = new ChangeTournamentTeamPaymentStatusCommand(tournamentId, tournamentTeamId, request.PaymentStatus);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPatch("{tournamentId}/tournament-teams/{tournamentTeamId}/group")]
        public async Task<IActionResult> AssignTeamToGroup([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromBody] AssignTeamToGroupRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignTeamToGroupCommand(tournamentId, tournamentTeamId, request.GroupId);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{tournamentId}/tournament-teams/{tournamentTeamId}/staff")]
        public async Task<IActionResult> AssignUserToTournamentTeam([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromBody] AssignUserToTournamentTeamRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignUserToTournamentTeamCommand(tournamentId, tournamentTeamId, request.UserId, request.Role);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/tournament-teams/{tournamentTeamId}/staff/{result.Value.TeamStaffId}", result.Value);
        }

        [HttpPost("{tournamentId}/tournament-teams/{tournamentTeamId}/coaches")]
        public async Task<IActionResult> AssignCoachToTeam([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromBody] AssignCoachToTeamRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignCoachToTeamCommand(tournamentId, tournamentTeamId, request.UserId);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/tournament-teams/{tournamentTeamId}/staff/{result.Value.TeamStaffId}", result.Value);
        }

        [HttpGet("{tournamentId}/tournament-teams/{tournamentTeamId}/staff")]
        public async Task<IActionResult> GetTeamStaff([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, CancellationToken cancellationToken)
        {
            var query = new GetTeamStaffQuery(tournamentId, tournamentTeamId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("{tournamentId}/tournament-teams/{tournamentTeamId}/players")]
        public async Task<IActionResult> AssignPlayerToTournamentTeam([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromBody] AssignPlayerToTournamentTeamRequest request, CancellationToken cancellationToken)
        {
            var command = new AssignPlayerToTournamentTeamCommand(tournamentId, tournamentTeamId, request.PlayerId, request.ShirtNumber, request.Position);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/tournament-teams/{tournamentTeamId}/players/{result.Value.TournamentPlayerId}", result.Value);
        }

        [HttpPost("{tournamentId}/tournament-teams/{tournamentTeamId}/players/new")]
        public async Task<IActionResult> CreatePlayerInTournamentTeam([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromBody] CreatePlayerInTournamentTeamRequest request, CancellationToken cancellationToken)
        {
            var command = new CreatePlayerInTournamentTeamCommand(
                tournamentId, tournamentTeamId, request.FirstName, request.LastName, request.BirthDate,
                request.DefaultPosition, request.PhotoUrl, request.ShirtNumber, request.Position);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return result.ToActionResult();

            return Created($"api/tournaments/{tournamentId}/tournament-teams/{tournamentTeamId}/players/{result.Value.TournamentPlayerId}", result.Value);
        }

        [HttpPatch("{tournamentId}/tournament-teams/{tournamentTeamId}/players/{tournamentPlayerId}")]
        public async Task<IActionResult> UpdateTournamentPlayer([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, [FromRoute] Guid tournamentPlayerId, [FromBody] UpdateTournamentPlayerRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateTournamentPlayerCommand(tournamentId, tournamentTeamId, tournamentPlayerId, request.ShirtNumber, request.Position, request.Status);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{tournamentId}/tournament-teams/{tournamentTeamId}/players")]
        public async Task<IActionResult> GetTournamentTeamPlayers([FromRoute] Guid tournamentId, [FromRoute] Guid tournamentTeamId, CancellationToken cancellationToken)
        {
            var query = new GetTournamentTeamPlayersQuery(tournamentId, tournamentTeamId);
            var result = await sender.Send(query, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record ChangeTournamentStatusRequest(TournamentStatus TournamentStatus);
        public sealed record UpdateTournamentFormatRequest(TournamentFormat TournamentFormat);
        public sealed record UpdateTournamentDetailsRequest(string Name, DateTimeOffset StartAt, DateTimeOffset EndAt, int TeamLimit, string? Description);
        public sealed record AddTournamentCategoryRequest(string Name, DateTime? MinBirthDate, DateTime? MaxBirthDate, int Order);
        public sealed record UpdateTournamentCategoryRequest(string Name, DateTime? MinBirthDate, DateTime? MaxBirthDate, int Order);
        public sealed record AddTournamentGroupRequest(Guid CategoryId, string Name, int Order);
        public sealed record UpdateTournamentGroupRequest(string Name, int Order);
        public sealed record SetTieBreakerRulesRequest(IReadOnlyList<TieBreakerRuleItem> Rules);
        public sealed record UpdateTournamentSettingsRequest(int PointsForWin, int PointsForDraw, int PointsForLoss, int MatchDurationMinutes, int BreakDurationMinutes, bool IsThirdPlaceMatchEnabled, int RefereeEditWindowMinutes, int WalkoverHomeScore, int WalkoverAwayScore, bool CanCoachEditSquadAfterStart);
        public sealed record UpdateTournamentPublicSettingsRequest(bool IsPublic, bool ShowTeams, bool ShowPlayers, bool ShowSchedule, bool ShowStandings, bool ShowBracket, bool ShowLiveMatches, bool ShowStats, bool AbbreviatePlayerNames = true);
        public sealed record AddTeamToTournamentRequest(Guid TeamId, Guid CategoryId);
        public sealed record ChangeTournamentTeamRegistrationStatusRequest(RegistrationStatus RegistrationStatus);
        public sealed record ChangeTournamentTeamPaymentStatusRequest(PaymentStatus PaymentStatus);
        public sealed record AssignTeamToGroupRequest(Guid? GroupId);
        public sealed record AssignUserToTournamentTeamRequest(Guid UserId, TeamStaffRole Role);
        public sealed record AssignCoachToTeamRequest(Guid UserId);
        public sealed record AssignPlayerToTournamentTeamRequest(Guid PlayerId, int? ShirtNumber, PlayerPosition? Position);
        public sealed record CreatePlayerInTournamentTeamRequest(string FirstName, string LastName, DateTime BirthDate, PlayerPosition DefaultPosition, string? PhotoUrl, int? ShirtNumber, PlayerPosition? Position);
        public sealed record UpdateTournamentPlayerRequest(int? ShirtNumber, PlayerPosition? Position, TournamentPlayerStatus Status);
    }
}