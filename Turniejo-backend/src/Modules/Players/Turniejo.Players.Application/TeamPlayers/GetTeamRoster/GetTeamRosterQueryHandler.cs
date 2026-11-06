using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TeamPlayers.GetTeamRoster
{
    internal sealed class GetTeamRosterQueryHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        TeamRosterAccessGuard accessGuard,
        TeamSquadsReader squadsReader,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetTeamRosterQuery, TeamRosterDto>
    {
        public async Task<Result<TeamRosterDto>> Handle(GetTeamRosterQuery request, CancellationToken cancellationToken)
        {
            var team = await teamModule.GetTeamAsync(request.TeamId, cancellationToken);
            if (team == null)
                return Result.Failure<TeamRosterDto>(Error.NotFound("Team", request.TeamId));

            var access = await accessGuard.GetAccessAsync(request.TeamId, currentUserContext.UserId, cancellationToken);
            if (access == TeamRosterAccess.None)
                throw new UnauthorizedException();

            var members = await dbContext.GetTeamPlayersByTeamIdAsync(request.TeamId, cancellationToken);
            var profilesById = (await dbContext.GetPlayersByIdsAsync(members.Select(m => m.PlayerId).ToList(), cancellationToken))
                .ToDictionary(p => p.Id);
            var squadsByPlayer = (await squadsReader.ReadAsync(request.TeamId, currentUserContext.UserId, cancellationToken))
                .ToLookup(e => e.TournamentPlayer.PlayerId);

            var players = members
                .Where(m => profilesById.ContainsKey(m.PlayerId))
                .Select(m =>
                {
                    var profile = profilesById[m.PlayerId];
                    var squads = squadsByPlayer[m.PlayerId].ToList();
                    return new TeamRosterPlayerDto(
                        profile.Id, profile.FirstName, profile.LastName, profile.BirthDate,
                        profile.DefaultPosition.ToString(), profile.PhotoUrl,
                        m.ShirtNumber, m.Status.ToString(), m.LeftAt,
                        squads.Where(s => s.IsCurrent)
                            .Select(s => s.Squad)
                            .OrderBy(s => s.TournamentStartAt ?? DateTimeOffset.MaxValue)
                            .ToList(),
                        squads.Where(s => !s.IsCurrent).Select(s => s.Squad.TournamentId).Distinct().Count());
                })
                .OrderBy(p => p.ShirtNumber ?? int.MaxValue)
                .ThenBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToList();

            return Result.Success(new TeamRosterDto(team.Id, team.Name, access == TeamRosterAccess.Edit, players));
        }
    }
}
