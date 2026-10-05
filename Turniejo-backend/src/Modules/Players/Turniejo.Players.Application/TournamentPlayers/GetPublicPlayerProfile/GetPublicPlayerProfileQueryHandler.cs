using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Helpers;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.GetPublicPlayerProfile
{
    internal sealed class GetPublicPlayerProfileQueryHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicPlayerProfileQuery, PublicPlayerProfileDto>
    {
        public async Task<Result<PublicPlayerProfileDto>> Handle(GetPublicPlayerProfileQuery request, CancellationToken cancellationToken)
        {
            var tournamentPlayer = await dbContext.GetTournamentPlayerByIdAsync(request.TournamentPlayerId, cancellationToken);
            if (tournamentPlayer == null)
                return Result.Failure<PublicPlayerProfileDto>(Error.NotFound("TournamentPlayer", request.TournamentPlayerId));

            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentPlayer.TournamentTeamId, cancellationToken);
            // Zamyka lukę z audytu: bez tego zawodnik drużyny w statusie Pending/Rejected (a więc
            // niewidocznej na publicznej liście drużyn — patrz GetPublicTeamProfileQueryHandler) był
            // i tak dostępny publicznie pod bezpośrednim linkiem do jego profilu.
            if (tournamentTeam == null || tournamentTeam.RegistrationStatus != "Accepted")
                return Result.Failure<PublicPlayerProfileDto>(Error.NotFound("TournamentPlayer", request.TournamentPlayerId));

            var settings = await tournamentModule.GetTournamentPublicSettingsAsync(tournamentTeam.TournamentId, cancellationToken);
            if (settings == null || !settings.IsPublic || !settings.ShowPlayers)
                return Result.Failure<PublicPlayerProfileDto>(Error.NotFound("TournamentPlayer", request.TournamentPlayerId));

            var player = await dbContext.GetPlayerByIdAsync(tournamentPlayer.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<PublicPlayerProfileDto>(Error.NotFound("TournamentPlayer", request.TournamentPlayerId));

            var team = await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken);

            var dto = new PublicPlayerProfileDto(
                tournamentPlayer.Id, player.FirstName,
                PublicPlayerName.LastName(player.LastName, settings.AbbreviatePlayerNames), player.PhotoUrl,
                tournamentPlayer.ShirtNumber, tournamentPlayer.Position?.ToString(),
                tournamentTeam.Id, team?.Name ?? "Unknown");

            return Result.Success(dto);
        }
    }
}
