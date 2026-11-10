using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Helpers;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.GetPublicTeamSquad
{
    /// <summary>
    /// Skład drużyny na stronie drużyny w portalu. Te same zasady co profil zawodnika: portal publiczny,
    /// włączone „Pokazuj drużyny” i „Pokazuj zawodników”, drużyna zaakceptowana w tym turnieju, nazwisko
    /// skrócone wg ustawień (RODO). Zawodnik usunięty ze składu nie jest pokazywany.
    /// </summary>
    internal sealed class GetPublicTeamSquadQueryHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicTeamSquadQuery, IReadOnlyList<PublicSquadPlayerDto>>
    {
        public async Task<Result<IReadOnlyList<PublicSquadPlayerDto>>> Handle(GetPublicTeamSquadQuery request, CancellationToken cancellationToken)
        {
            var notFound = Result.Failure<IReadOnlyList<PublicSquadPlayerDto>>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<PublicSquadPlayerDto>>(Error.NotFound("Tournament", request.Slug));
            if (!tournament.Settings.ShowTeams || !tournament.Settings.ShowPlayers)
                return notFound;

            var tournamentTeam = await teamModule.GetTournamentTeamAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != tournament.Id || tournamentTeam.RegistrationStatus != "Accepted")
                return notFound;

            var tournamentPlayers = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeam.Id, cancellationToken);
            var inSquad = tournamentPlayers.Where(tp => tp.Status != TournamentPlayerStatus.Removed).ToList();
            var players = (await dbContext.GetPlayersByIdsAsync(inSquad.Select(tp => tp.PlayerId).ToList(), cancellationToken))
                .ToDictionary(p => p.Id);

            IReadOnlyList<PublicSquadPlayerDto> squad = inSquad
                .Where(tp => players.ContainsKey(tp.PlayerId))
                .Select(tp => new PublicSquadPlayerDto(
                    tp.Id, tp.PlayerId, players[tp.PlayerId].FirstName,
                    PublicPlayerName.LastName(players[tp.PlayerId].LastName, tournament.Settings.AbbreviatePlayerNames),
                    tp.ShirtNumber, tp.Position?.ToString()))
                .OrderBy(p => p.ShirtNumber ?? int.MaxValue)
                .ThenBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToList();

            return Result.Success(squad);
        }
    }
}
