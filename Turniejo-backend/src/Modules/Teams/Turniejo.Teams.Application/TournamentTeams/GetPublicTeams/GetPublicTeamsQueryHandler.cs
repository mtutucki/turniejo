using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.GetPublicTeams
{
    internal sealed class GetPublicTeamsQueryHandler(
        ITeamsDbContext dbContext,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicTeamsQuery, IReadOnlyList<PublicTeamDto>>
    {
        public async Task<Result<IReadOnlyList<PublicTeamDto>>> Handle(GetPublicTeamsQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<PublicTeamDto>>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowTeams)
                return Result.Success<IReadOnlyList<PublicTeamDto>>([]);

            var tournamentTeams = await dbContext.GetTournamentTeamsByTournamentIdAsync(tournament.Id, cancellationToken);
            var acceptedTeams = tournamentTeams.Where(t => t.RegistrationStatus == RegistrationStatus.Accepted).ToList();

            var dtos = new List<PublicTeamDto>();
            foreach (var tournamentTeam in acceptedTeams)
            {
                var team = await dbContext.GetTeamByIdAsync(tournamentTeam.TeamId, cancellationToken);
                dtos.Add(new PublicTeamDto(
                    tournamentTeam.Id, tournamentTeam.TeamId, team?.Name ?? "Unknown", team?.ShortName,
                    tournamentTeam.CategoryId, tournamentTeam.GroupId));
            }

            return Result.Success<IReadOnlyList<PublicTeamDto>>(dtos);
        }
    }
}
