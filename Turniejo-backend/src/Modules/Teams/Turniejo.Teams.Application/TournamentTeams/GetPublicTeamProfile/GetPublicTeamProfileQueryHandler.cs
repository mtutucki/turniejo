using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.GetPublicTeamProfile
{
    internal sealed class GetPublicTeamProfileQueryHandler(
        ITeamsDbContext dbContext,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicTeamProfileQuery, PublicTeamDto>
    {
        public async Task<Result<PublicTeamDto>> Handle(GetPublicTeamProfileQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<PublicTeamDto>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowTeams)
                return Result.Failure<PublicTeamDto>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != tournament.Id || tournamentTeam.RegistrationStatus != RegistrationStatus.Accepted)
                return Result.Failure<PublicTeamDto>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var team = await dbContext.GetTeamByIdAsync(tournamentTeam.TeamId, cancellationToken);

            var dto = new PublicTeamDto(
                tournamentTeam.Id, tournamentTeam.TeamId, team?.Name ?? "Unknown", team?.ShortName,
                tournamentTeam.CategoryId, tournamentTeam.GroupId);

            return Result.Success(dto);
        }
    }
}
