using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournamentById
{
    internal sealed class GetTournamentByIdQueryHandler(
        ITournamentsDbContext tournamentsDbContext,
        Turniejo.SharedKernel.Contracts.ICurrentUserContext currentUserContext,
        Turniejo.SharedKernel.Contracts.IAuthorizationService authorizationService) : IQueryHandler<GetTournamentByIdQuery, TournamentDto>
    {
        public async Task<Result<TournamentDto>> Handle(GetTournamentByIdQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (tournament == null)
                return Result.Failure<TournamentDto>(Error.NotFound("Tournament", request.TournamentId));

            // Query jest dostępne dla każdego członka turnieju (AnyMember), ale kody pozwalają dołączyć
            // w roli Sędziego/Trenera — bez tego Viewer mógłby odczytać RefereeCode i sam się awansować.
            var isOrganizer = await authorizationService.HasAnyRoleAsync(
                currentUserContext.UserId,
                nameof(RoleScopeType.Tournament),
                tournament.Id,
                TournamentRoles.OrganizerOnly,
                cancellationToken);

            var tournamentDto = new TournamentDto
            (
                tournament.Id,
                tournament.Name,
                tournament.Slug,
                tournament.Status.ToString(),
                tournament.Format?.ToString(),
                tournament.StartAt,
                tournament.EndAt,
                tournament.TeamLimit,
                tournament.IsPaid,
                isOrganizer ? tournament.RefereeCode : null,
                isOrganizer ? tournament.CoachCode : null,
                isOrganizer ? tournament.ViewerCode : null,
                tournament.Description,
                tournament.PublicSettings.IsPublic
            );

            return Result.Success(tournamentDto);
        }
    }
}
