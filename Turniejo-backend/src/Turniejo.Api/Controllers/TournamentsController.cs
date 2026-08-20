using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Tournaments.Application.TieBreakerRules.SetTieBreakerRules;
using Turniejo.Tournaments.Application.TournamentGroups.AddTournamentGroup;
using Turniejo.Tournaments.Application.TournamentGroups.GetTournamentGroups;
using Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentById;
using Turniejo.Tournaments.Application.Tournaments.GetTournaments;
using Turniejo.Tournaments.Application.Tournaments.JoinTournament;
using Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat;
using Turniejo.Tournaments.Application.Tournaments.UpdateTournament;
using Turniejo.Tournaments.Application.TournamentsCategories.AddTournamentCategory;
using Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory;
using Turniejo.Tournaments.Application.TournamentsCategories.GetTournamentCategories;
using Turniejo.Tournaments.Application.TournamentsCategories.UpdateTournamentCategory;
using Turniejo.Tournaments.Domain.Enums;

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

        [HttpPatch("{tournamentId}/status")]
        public async Task<IActionResult> ChangeTournamentStatus([FromRoute] Guid tournamentId, [FromBody] ChangeTournamentStatusRequest request, CancellationToken cancellationToken)
        {
            var command = new ChangeTournamentStatusCommand(tournamentId, request.TournamentStatus);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
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
            var command = new UpdateTournamentCategoryCommand(categoryId, request.Name, request.MinBirthDate, request.MaxBirthDate, request.Order);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{tournamentId}/categories/{categoryId}")]
        public async Task<IActionResult> DeleteTournamentCategory([FromRoute] Guid tournamentId, [FromRoute] Guid categoryId, CancellationToken cancellationToken)
        {
            var command = new DeleteTournamentCategoryCommand(categoryId);
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

        [HttpPut("{tournamentId}/tiebreaker-rules")]
        public async Task<IActionResult> SetTieBreakerRules([FromRoute] Guid tournamentId, [FromBody] SetTieBreakerRulesRequest request, CancellationToken cancellationToken)
        {
            var command = new SetTieBreakerRulesCommand(tournamentId, request.Rules);
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        public sealed record ChangeTournamentStatusRequest(TournamentStatus TournamentStatus);
        public sealed record UpdateTournamentFormatRequest(TournamentFormat TournamentFormat);
        public sealed record UpdateTournamentDetailsRequest(string Name, DateTimeOffset StartAt, DateTimeOffset EndAt, int TeamLimit, string? Description);
        public sealed record AddTournamentCategoryRequest(string Name, DateTime? MinBirthDate, DateTime? MaxBirthDate, int Order);
        public sealed record UpdateTournamentCategoryRequest(string Name, DateTime? MinBirthDate, DateTime? MaxBirthDate, int Order);
        public sealed record AddTournamentGroupRequest(Guid CategoryId, string Name, int Order);
        public sealed record SetTieBreakerRulesRequest(IReadOnlyList<TieBreakerRuleItem> Rules);
    }
}