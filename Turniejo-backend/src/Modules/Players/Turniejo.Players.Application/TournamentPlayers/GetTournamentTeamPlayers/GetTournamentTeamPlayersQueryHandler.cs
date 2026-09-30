using Turniejo.SharedKernel.Contracts;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers
{
    internal sealed class GetTournamentTeamPlayersQueryHandler(IPlayersDbContext dbContext, ITeamModule teamModule) : IQueryHandler<GetTournamentTeamPlayersQuery, IReadOnlyList<TournamentPlayerDto>>
    {
        public async Task<Result<IReadOnlyList<TournamentPlayerDto>>> Handle(GetTournamentTeamPlayersQuery request, CancellationToken cancellationToken)
        {
            // AuthorizationBehavior sprawdza rolę w turnieju z adresu, ale drużyna pochodzi od klienta — bez tego
            // członek dowolnego turnieju (nawet kibic z kodem) czytał skład drużyny z innego turnieju, razem
            // z datami urodzenia dzieci. Guid.Empty: trasa trenera (/api/coach/teams/{id}/players) — tam
            // dostęp daje wyłącznie alternatywny zakres Coach@TournamentTeam.
            if (request.TournamentId != Guid.Empty)
            {
                var tournamentTeam = await teamModule.GetTournamentTeamAsync(request.TournamentTeamId, cancellationToken);
                if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                    return Result.Failure<IReadOnlyList<TournamentPlayerDto>>(Error.NotFound("TournamentTeam", request.TournamentTeamId));
            }

            var players = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(request.TournamentTeamId, cancellationToken);

            var playerIds = players.Select(p => p.PlayerId).Distinct().ToList();
            var profilesById = (await dbContext.GetPlayersByIdsAsync(playerIds, cancellationToken)).ToDictionary(p => p.Id);

            var dtos = players.Select(p =>
            {
                profilesById.TryGetValue(p.PlayerId, out var profile);
                return new TournamentPlayerDto(
                    p.Id, p.TournamentTeamId, p.PlayerId, p.ShirtNumber,
                    p.Position?.ToString(), p.Status.ToString(),
                    profile?.FirstName ?? string.Empty, profile?.LastName ?? string.Empty,
                    profile?.BirthDate, profile?.PhotoUrl);
            }).ToList();

            return Result.Success<IReadOnlyList<TournamentPlayerDto>>(dtos);
        }
    }
}
