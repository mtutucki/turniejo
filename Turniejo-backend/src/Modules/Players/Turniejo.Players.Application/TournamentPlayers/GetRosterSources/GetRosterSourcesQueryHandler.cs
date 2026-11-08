using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.GetRosterSources
{
    internal sealed class GetRosterSourcesQueryHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule) : IQueryHandler<GetRosterSourcesQuery, IReadOnlyList<RosterSourceDto>>
    {
        public async Task<Result<IReadOnlyList<RosterSourceDto>>> Handle(GetRosterSourcesQuery request, CancellationToken cancellationToken)
        {
            // Jak w pozostałych handlerach składu: pipeline weryfikuje tylko jeden z zakresów (turniej albo
            // drużynę), więc spójność pary turniej–drużyna sprawdzamy tutaj.
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<IReadOnlyList<RosterSourceDto>>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var registrations = await teamModule.GetTournamentTeamsByTeamIdAsync(tournamentTeam.TeamId, cancellationToken);
            var category = await tournamentModule.GetTournamentCategoryAsync(tournamentTeam.CategoryId, cancellationToken);

            var currentSquad = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeam.Id, cancellationToken);
            var currentPlayerIds = currentSquad.Select(tp => tp.PlayerId).ToHashSet();

            // Kto odszedł z kadry drużyny, nie jest proponowany także ze składów poprzednich turniejów
            // (QA v3: DRU-027a). Zawodnicy spoza kadry (dane sprzed kadry) dalej są proponowani.
            var formerMembers = (await dbContext.GetTeamPlayersByTeamIdAsync(tournamentTeam.TeamId, cancellationToken))
                .Where(tp => tp.Status == TeamPlayerStatus.Left)
                .Select(tp => tp.PlayerId)
                .ToHashSet();

            var sources = new List<RosterSourceDto>();

            foreach (var registration in registrations.Where(r => r.Id != tournamentTeam.Id))
            {
                var roster = (await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(registration.Id, cancellationToken))
                    .Where(tp => tp.Status != TournamentPlayerStatus.Removed && !formerMembers.Contains(tp.PlayerId))
                    .ToList();
                if (roster.Count == 0)
                    continue;

                var profilesById = (await dbContext.GetPlayersByIdsAsync(roster.Select(tp => tp.PlayerId).ToList(), cancellationToken))
                    .ToDictionary(p => p.Id);

                var players = roster
                    .Where(tp => profilesById.ContainsKey(tp.PlayerId))
                    .Select(tp =>
                    {
                        var profile = profilesById[tp.PlayerId];
                        return new RosterSourcePlayerDto(
                            profile.Id, profile.FirstName, profile.LastName, profile.BirthDate,
                            tp.ShirtNumber, tp.Position?.ToString(),
                            currentPlayerIds.Contains(profile.Id),
                            CategoryAgeRule.Check(category, profile.BirthDate));
                    })
                    .OrderBy(p => p.ShirtNumber ?? int.MaxValue)
                    .ThenBy(p => p.LastName)
                    .ToList();
                if (players.Count == 0)
                    continue;

                var tournament = await tournamentModule.GetTournamentAsync(registration.TournamentId, cancellationToken);
                var sourceCategory = await tournamentModule.GetTournamentCategoryAsync(registration.CategoryId, cancellationToken);

                sources.Add(new RosterSourceDto(
                    registration.Id, registration.TournamentId,
                    tournament?.Name ?? string.Empty, tournament?.StartAt,
                    sourceCategory?.Name, players));
            }

            // Kadra pierwsza (domyślna), potem najnowszy turniej.
            var ordered = sources.OrderByDescending(s => s.TournamentStartAt ?? DateTimeOffset.MinValue).ToList();
            var teamRoster = await BuildRosterSourceAsync(tournamentTeam.TeamId, category, currentPlayerIds, cancellationToken);
            if (teamRoster != null)
                ordered.Insert(0, teamRoster);

            return Result.Success<IReadOnlyList<RosterSourceDto>>(ordered);
        }

        private async Task<RosterSourceDto?> BuildRosterSourceAsync(
            Guid teamId, TournamentCategoryDto? category, HashSet<Guid> currentPlayerIds, CancellationToken cancellationToken)
        {
            var members = (await dbContext.GetTeamPlayersByTeamIdAsync(teamId, cancellationToken))
                .Where(tp => tp.Status == TeamPlayerStatus.Active)
                .ToList();
            if (members.Count == 0)
                return null;

            var profilesById = (await dbContext.GetPlayersByIdsAsync(members.Select(m => m.PlayerId).ToList(), cancellationToken))
                .ToDictionary(p => p.Id);

            var players = members
                .Where(m => profilesById.ContainsKey(m.PlayerId))
                .Select(m =>
                {
                    var profile = profilesById[m.PlayerId];
                    return new RosterSourcePlayerDto(
                        profile.Id, profile.FirstName, profile.LastName, profile.BirthDate,
                        m.ShirtNumber, profile.DefaultPosition.ToString(),
                        currentPlayerIds.Contains(profile.Id),
                        CategoryAgeRule.Check(category, profile.BirthDate));
                })
                .OrderBy(p => p.ShirtNumber ?? int.MaxValue)
                .ThenBy(p => p.LastName)
                .ToList();
            if (players.Count == 0)
                return null;

            return new RosterSourceDto(null, null, string.Empty, null, null, players, RosterSourceDto.RosterKind);
        }
    }
}
