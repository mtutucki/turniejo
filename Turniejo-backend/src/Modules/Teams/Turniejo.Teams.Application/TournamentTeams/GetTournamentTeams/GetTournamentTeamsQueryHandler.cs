using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetTournamentTeams
{
    internal sealed class GetTournamentTeamsQueryHandler(ITeamsDbContext dbContext) : IQueryHandler<GetTournamentTeamsQuery, IReadOnlyList<TournamentTeamDto>>
    {
        public async Task<Result<IReadOnlyList<TournamentTeamDto>>> Handle(GetTournamentTeamsQuery request, CancellationToken cancellationToken)
        {
            var tournamentTeams = await dbContext.GetTournamentTeamsByTournamentIdAsync(request.TournamentId, cancellationToken);

            var dtos = tournamentTeams.Select(t => new TournamentTeamDto(
                t.Id, t.TournamentId, t.TeamId, t.CategoryId, t.GroupId,
                t.RegistrationStatus.ToString(), t.PaymentStatus.ToString(), t.SeedNumber)).ToList();

            return Result.Success<IReadOnlyList<TournamentTeamDto>>(dtos);
        }
    }
}
