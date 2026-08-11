using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournamentById
{
    internal sealed class GetTournamentByIdQueryHandler(ITournamentsDbContext tournamentsDbContext) : IQueryHandler<GetTournamentByIdQuery, TournamentDto>
    {
        public async Task<Result<TournamentDto>> Handle(GetTournamentByIdQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (tournament == null)
                return Result.Failure<TournamentDto>(Error.NotFound("Tournament", request.TournamentId));

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
                tournament.OrganizerCode,
                tournament.RefereeCode,
                tournament.CoachCode,
                tournament.ViewerCode
            );

            return Result.Success(tournamentDto);
        }
    }
}
