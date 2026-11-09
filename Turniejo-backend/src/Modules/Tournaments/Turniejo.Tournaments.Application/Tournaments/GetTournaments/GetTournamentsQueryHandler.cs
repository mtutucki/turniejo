using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournaments
{
    internal sealed class GetTournamentsQueryHandler(ITournamentsDbContext dbContext) : IQueryHandler<GetTournamentsQuery, IReadOnlyList<TournamentDto>>
    {
        public async Task<Result<IReadOnlyList<TournamentDto>>> Handle(
            GetTournamentsQuery request,
            CancellationToken cancellationToken)
        {
            var tournaments = await dbContext.GetTournamentsByOrganizationIdAsync(
                request.OrganizationId, cancellationToken);

            var dtos = tournaments.Select(t => new TournamentDto(
                t.Id, t.Name, t.Slug,
                t.Status.ToString(),
                t.Format?.ToString(),
                t.StartAt, t.EndAt,
                t.TeamLimit, t.IsPaid,
                t.RefereeCode,
                t.CoachCode, t.ViewerCode, t.Description,
                t.PublicSettings.IsPublic, t.UpdatedAt)).ToList();

            return Result.Success<IReadOnlyList<TournamentDto>>(dtos);
        }
    }
}
