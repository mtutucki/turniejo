using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Schedule.GenerateSchedule
{
    // Dwa tryby wg formatu turnieju:
    // - Group / GroupAndKnockout: tworzy mecze fazy grupowej (każdy z każdym) i rozkłada je w oknie czasu,
    // - Knockout: mecze 1. rundy tworzy drabinka (GenerateBracket) — tu tylko dostają termin i boisko
    //   w oknie czasu. Kolejne rundy powstają po rozstrzygnięciu meczów (CreateBracketMatch).
    // W obu przypadkach na końcu etap turnieju zmienia się na ScheduleGenerated.
    internal sealed class GenerateScheduleCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        ITeamModule teamModule,
        IBracketModule bracketModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<GenerateScheduleCommand, GenerateScheduleResponse>
    {
        private sealed record MatchPair(Guid CategoryId, Guid GroupId, Guid HomeTournamentTeamId, Guid AwayTournamentTeamId);

        private sealed record Placement<T>(T Item, Guid PitchId, DateTimeOffset ScheduledAt);

        private static readonly StageType[] BracketStages = [StageType.Knockout, StageType.ThirdPlace, StageType.Final];

        public async Task<Result<GenerateScheduleResponse>> Handle(GenerateScheduleCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<GenerateScheduleResponse>(Error.NotFound("Tournament", request.TournamentId));

            // Sprawdzane na starcie, a nie dopiero przy zmianie statusu na końcu — inaczej użytkownik dostawał
            // błędy o grupach/boiskach zamiast właściwej przyczyny (zapisy wciąż otwarte, harmonogram już jest).
            if (tournament.Status != "RegistrationClosed")
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("Status", "Schedule can only be generated when registration is closed"));

            var windows = request.EffectiveWindows;
            if (tournament.StartAt is { } tournamentStart && tournament.EndAt is { } tournamentEnd &&
                windows.Any(w => w.StartAt < tournamentStart || w.EndAt > tournamentEnd))
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("StartAt", "Schedule must fit within the tournament dates"));

            if (tournament.Format is not ("Group" or "GroupAndKnockout" or "Knockout"))
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("Format", "Set the tournament format before generating the schedule"));

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<GenerateScheduleResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            var allPitches = await dbContext.GetPitchesByIdsAsync(request.PitchIds, cancellationToken);
            var pitches = allPitches.Where(p => p.TournamentId == request.TournamentId && p.IsActive).ToList();
            if (pitches.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("PitchIds", "No valid active pitches belonging to this tournament were provided"));

            var existingMatches = await dbContext.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken);

            return tournament.Format == "Knockout"
                ? await ScheduleKnockoutAsync(request, windows, settings, pitches, existingMatches, cancellationToken)
                : await ScheduleGroupStageAsync(request, windows, settings, pitches, existingMatches, cancellationToken);
        }

        private async Task<Result<GenerateScheduleResponse>> ScheduleGroupStageAsync(
            GenerateScheduleCommand request,
            IReadOnlyList<ScheduleWindow> windows,
            TournamentSettingsSummaryDto settings,
            List<Pitch> pitches,
            IReadOnlyList<Match> existingMatches,
            CancellationToken cancellationToken)
        {
            var groups = await tournamentModule.GetTournamentGroupsAsync(request.TournamentId, cancellationToken);
            if (groups.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("TournamentId", "Tournament has no groups defined"));

            var allTournamentTeams = await teamModule.GetTournamentTeamsByTournamentIdAsync(request.TournamentId, cancellationToken);
            // Mecze grają tylko drużyny przypisane do grupy. Zaakceptowana drużyna bez grupy zostałaby po
            // cichu pominięta, a terminarz generuje się raz — organizator zauważał to dopiero po fakcie.
            if (allTournamentTeams.Any(t => t.RegistrationStatus == "Accepted" && !t.GroupId.HasValue))
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("TournamentId", "Some accepted teams are not assigned to a group"));

            var acceptedTeamsByGroup = allTournamentTeams
                .Where(t => t.RegistrationStatus == "Accepted" && t.GroupId.HasValue)
                .GroupBy(t => t.GroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());

            var pairs = new List<MatchPair>();
            var roundsByGroup = new List<List<List<MatchPair>>>();
            foreach (var group in groups)
            {
                if (!acceptedTeamsByGroup.TryGetValue(group.Id, out var teamIds) || teamIds.Count < 2)
                    continue;

                roundsByGroup.Add(BuildRoundRobinRounds(group, teamIds));
            }

            // Kolejka 1 wszystkich grup, potem kolejka 2 itd. — grupy grają równolegle, a drużyny dostają przerwy.
            var maxRounds = roundsByGroup.Count == 0 ? 0 : roundsByGroup.Max(r => r.Count);
            for (var round = 0; round < maxRounds; round++)
                foreach (var groupRounds in roundsByGroup)
                    if (round < groupRounds.Count)
                        pairs.AddRange(groupRounds[round]);

            if (pairs.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("TournamentId", "No matches to schedule — each group needs at least 2 accepted teams"));

            // Pozostałość po nieudanym generowaniu (mecze zapisane, etap nie zmieniony) — ponowne generowanie
            // zdublowałoby pary. Organizator czyści terminarz (ClearSchedule) i generuje od nowa.
            if (existingMatches.Any(m => m.StageType == StageType.Group && m.Status != MatchStatus.Cancelled))
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("TournamentId", "Tournament already has group matches — clear the schedule first"));

            var occupiedSlots = existingMatches
                .Where(m => m.PitchId.HasValue && m.Status != MatchStatus.Cancelled)
                .Select(m => (m.PitchId!.Value, m.ScheduledAt))
                .ToHashSet();

            var clubOf = ClubLookup(allTournamentTeams);
            var (placements, unplaced) = PlaceInSlots(
                pairs, p => (clubOf(p.HomeTournamentTeamId), clubOf(p.AwayTournamentTeamId)), pitches, occupiedSlots, windows, settings);
            if (unplaced > 0)
                return NotEnoughTime(unplaced);

            var scheduledMatches = new List<Match>();
            foreach (var placement in placements)
            {
                var pair = placement.Item;
                var matchResult = Match.Create(
                    Guid.NewGuid(), request.TournamentId, pair.CategoryId, pair.GroupId,
                    pair.HomeTournamentTeamId, pair.AwayTournamentTeamId, placement.PitchId,
                    placement.ScheduledAt, StageType.Group);

                if (matchResult.IsFailure)
                    return Result.Failure<GenerateScheduleResponse>(matchResult.Error);

                scheduledMatches.Add(matchResult.Value);
            }

            // Mecze i etap turnieju żyją w dwóch bazach modułów, więc nie ma jednej transakcji. Kolejność:
            // najpierw mecze, potem etap — a gdy zmiana etapu się nie uda, usuwamy właśnie zapisane mecze.
            // Odwrotnie (etap pierwszy) błąd zapisu meczów zostawiał "harmonogram gotowy" z zerem meczów.
            await dbContext.AddMatchesAsync(scheduledMatches, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!await tournamentModule.ChangeTournamentStatusAsync(request.TournamentId, "ScheduleGenerated", cancellationToken))
            {
                await dbContext.RemoveMatchesAsync(scheduledMatches, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                return StatusConflict();
            }

            return await CompleteAsync(request.TournamentId, scheduledMatches.Count, cancellationToken);
        }

        private async Task<Result<GenerateScheduleResponse>> ScheduleKnockoutAsync(
            GenerateScheduleCommand request,
            IReadOnlyList<ScheduleWindow> windows,
            TournamentSettingsSummaryDto settings,
            List<Pitch> pitches,
            IReadOnlyList<Match> existingMatches,
            CancellationToken cancellationToken)
        {
            // Mecze drabinki, które jeszcze się nie zaczęły — w praktyce 1. runda (kolejne rundy powstają
            // dopiero po rozstrzygnięciu meczów). Kolejność z drabinki (termin utworzenia) zostaje.
            var toScheduleIds = existingMatches
                .Where(m => BracketStages.Contains(m.StageType) &&
                            m.Status is MatchStatus.Scheduled or MatchStatus.Ready or MatchStatus.Postponed)
                .Select(m => m.Id)
                .ToHashSet();
            if (toScheduleIds.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation(
                    "Bracket", "Generate the knockout bracket first — the schedule places its first-round matches"));

            // existingMatches jest tylko do odczytu (AsNoTracking) — zmiany na nich nie trafiały do bazy:
            // etap zmieniał się na "harmonogram gotowy", a mecze zostawały bez boiska, wszystkie o tej samej godzinie.
            // Najpierw wcześniejsze rundy: przy wolnych losach mecz 2. rundy powstaje od razu (przed
            // meczami 1. rundy) i bez tego dostawałby wcześniejszą godzinę niż one.
            var nodes = await bracketModule.GetNodesByTournamentIdAsync(request.TournamentId, cancellationToken);
            var roundByMatchId = nodes
                .Where(n => n.MatchId.HasValue)
                .ToDictionary(n => n.MatchId!.Value, n => n.RoundNumber);
            var toSchedule = (await dbContext.GetTrackedMatchesByIdsAsync(toScheduleIds, cancellationToken))
                .OrderBy(m => roundByMatchId.GetValueOrDefault(m.Id, int.MaxValue))
                .ThenBy(m => m.ScheduledAt)
                .ThenBy(m => m.CreatedAt)
                .ToList();

            var occupiedSlots = existingMatches
                .Where(m => m.PitchId.HasValue && m.Status != MatchStatus.Cancelled && !toScheduleIds.Contains(m.Id))
                .Select(m => (m.PitchId!.Value, m.ScheduledAt))
                .ToHashSet();

            var clubOf = ClubLookup(await teamModule.GetTournamentTeamsByTournamentIdAsync(request.TournamentId, cancellationToken));
            var (placements, unplaced) = PlaceInSlots(
                toSchedule, m => (clubOf(m.HomeTournamentTeamId), clubOf(m.AwayTournamentTeamId)), pitches, occupiedSlots, windows, settings);
            if (unplaced > 0)
                return NotEnoughTime(unplaced);

            var (plannedSlots, unplanned) = PlanLaterRounds(
                windows, settings, pitches, nodes, existingMatches, placements, occupiedSlots);
            if (unplanned > 0)
                return NotEnoughTime(unplanned);

            // Zapamiętane na wypadek wycofania, gdy etap turnieju nie da się zmienić.
            var previous = toSchedule.ToDictionary(m => m.Id, m => (m.PitchId, m.ScheduledAt));
            // Bez MatchScheduleChanged w outboxie: terminarz dopiero powstaje, to nie "zmiana terminu"
            // do rozesłania rodzicom (tak samo nowe mecze fazy grupowej nie wysyłają powiadomień).
            await ApplySlotsAsync(
                placements.Select(p => (p.Item, (Guid?)p.PitchId, p.ScheduledAt)).ToList(), cancellationToken);

            if (!await tournamentModule.ChangeTournamentStatusAsync(request.TournamentId, "ScheduleGenerated", cancellationToken))
            {
                await ApplySlotsAsync(
                    toSchedule.Select(m => (m, previous[m.Id].PitchId, previous[m.Id].ScheduledAt)).ToList(), cancellationToken);
                return StatusConflict();
            }

            await bracketModule.SetPlannedSlotsAsync(plannedSlots, cancellationToken);

            return await CompleteAsync(request.TournamentId, toSchedule.Count, cancellationToken);
        }

        // Dwa zapisy: najpierw mecze schodzą z boisk, dopiero potem dostają nowe terminy. Po wyczyszczeniu
        // terminarza mecze pucharu zachowują stare boisko i godzinę, więc nowy termin jednego meczu bywa
        // starym terminem innego — a unikalny indeks (boisko, godzina) sprawdza każdy UPDATE osobno,
        // więc taka zamiana w jednym zapisie kończyła się "Another match is already scheduled...".
        private async Task ApplySlotsAsync(
            List<(Match Match, Guid? PitchId, DateTimeOffset ScheduledAt)> slots, CancellationToken cancellationToken)
        {
            foreach (var (match, _, _) in slots)
                match.UpdateSchedule(null, match.ScheduledAt, currentUserContext.UserId);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var (match, pitchId, scheduledAt) in slots)
                match.UpdateSchedule(pitchId, scheduledAt, currentUserContext.UserId);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Terminy dla meczów kolejnych rund, które jeszcze nie istnieją (czekają na zwycięzców): runda po
        /// rundzie, każdy najwcześniej po meczach, z których przyjdą drużyny, na pierwszym wolnym boisku.
        /// </summary>
        private static (List<BracketPlannedSlotDto> Planned, int Unplanned) PlanLaterRounds(
            IReadOnlyList<ScheduleWindow> windows,
            TournamentSettingsSummaryDto settings,
            List<Pitch> pitches,
            IReadOnlyList<BracketNodeInfoDto> nodes,
            IReadOnlyList<Match> existingMatches,
            List<Placement<Match>> placements,
            HashSet<(Guid PitchId, DateTimeOffset ScheduledAt)> occupiedSlots)
        {
            var slot = TimeSpan.FromMinutes(settings.MatchDurationMinutes + settings.BreakDurationMinutes);
            var slotStarts = SlotStarts(windows, settings).Select(s => s.SlotStart).ToList();

            // Koniec meczu (z przerwą) każdego węzła, który ma już mecz: nowy termin z tego generowania
            // albo dotychczasowy (mecz, którego generator nie rusza).
            var matchStart = existingMatches
                .Where(m => m.Status != MatchStatus.Cancelled)
                .ToDictionary(m => m.Id, m => m.ScheduledAt);
            foreach (var placement in placements)
                matchStart[placement.Item.Id] = placement.ScheduledAt;

            var readyAt = new Dictionary<Guid, DateTimeOffset>();
            foreach (var node in nodes)
            {
                if (node.MatchId.HasValue && matchStart.TryGetValue(node.MatchId.Value, out var start))
                    readyAt[node.Id] = start + slot;
            }

            var planned = new List<BracketPlannedSlotDto>();
            var unplanned = 0;

            // Mecz o 3. miejsce ma numer rundy finału — idzie przed finałem.
            foreach (var node in nodes
                         .Where(n => !n.MatchId.HasValue && !n.HasBye)
                         .OrderBy(n => n.RoundNumber)
                         .ThenByDescending(n => n.IsThirdPlace))
            {
                var notBefore = new[] { node.HomeSourceNodeId, node.AwaySourceNodeId }
                    .Where(id => id.HasValue && readyAt.ContainsKey(id.Value))
                    .Select(id => readyAt[id!.Value])
                    .DefaultIfEmpty(windows[0].StartAt)
                    .Max();

                BracketPlannedSlotDto? found = null;
                foreach (var slotStart in slotStarts)
                {
                    if (slotStart < notBefore)
                        continue;

                    var pitch = pitches.FirstOrDefault(p => !occupiedSlots.Contains((p.Id, slotStart)));
                    if (pitch == null)
                        continue;

                    found = new BracketPlannedSlotDto(node.Id, pitch.Id, slotStart);
                    break;
                }

                if (found == null)
                {
                    unplanned++;
                    continue;
                }

                planned.Add(found);
                occupiedSlots.Add((found.PitchId, found.ScheduledAt));
                readyAt[node.Id] = found.ScheduledAt + slot;
            }

            return (planned, unplanned);
        }

        /// <summary>
        /// Początki slotów (mecz + przerwa) we wszystkich oknach po kolei. Mecz musi się zmieścić w oknie
        /// w całości, nie tylko zacząć przed jego końcem.
        /// </summary>
        private static IEnumerable<(DateTimeOffset SlotStart, int WindowIndex)> SlotStarts(
            IReadOnlyList<ScheduleWindow> windows, TournamentSettingsSummaryDto settings)
        {
            var matchDuration = TimeSpan.FromMinutes(settings.MatchDurationMinutes);
            var slotMinutes = settings.MatchDurationMinutes + settings.BreakDurationMinutes;

            for (var i = 0; i < windows.Count; i++)
                for (var slotStart = windows[i].StartAt;
                     slotStart + matchDuration <= windows[i].EndAt;
                     slotStart = slotStart.AddMinutes(slotMinutes))
                    yield return (slotStart, i);
        }

        /// <summary>
        /// Rozkłada mecze w slotach kolejnych okien (dni gry) na wolnych boiskach. Drużyna nigdy
        /// nie gra dwóch meczów w jednym slocie, a pierwszeństwo mają pary, których drużyny odpoczywały.
        /// Okno z limitem (MaxMatches) dostaje najwyżej tyle meczów — reszta przechodzi na kolejne dni.
        /// </summary>
        private static (List<Placement<T>> Placements, int Unplaced) PlaceInSlots<T>(
            List<T> items,
            Func<T, (Guid Home, Guid Away)> teamsOf,
            List<Pitch> pitches,
            HashSet<(Guid PitchId, DateTimeOffset ScheduledAt)> occupiedSlots,
            IReadOnlyList<ScheduleWindow> windows,
            TournamentSettingsSummaryDto settings)
        {
            var placements = new List<Placement<T>>();
            var remaining = new List<T>(items);
            var playedPreviousSlot = new HashSet<Guid>();
            var previousWindow = 0;
            var placedInWindow = new int[windows.Count];

            foreach (var (slotStart, windowIndex) in SlotStarts(windows, settings))
            {
                if (remaining.Count == 0)
                    break;

                // Nowy dzień — wszyscy są wypoczęci.
                if (windowIndex != previousWindow)
                    playedPreviousSlot = [];
                previousWindow = windowIndex;

                var windowLimit = (windows[windowIndex].MaxMatches ?? int.MaxValue) - placedInWindow[windowIndex];
                if (windowLimit <= 0)
                    continue;

                var freePitches = pitches.Where(p => !occupiedSlots.Contains((p.Id, slotStart))).ToList();
                var slotCapacity = Math.Min(freePitches.Count, windowLimit);
                var playingThisSlot = new HashSet<Guid>();
                var slotItems = new List<T>();

                foreach (var requireRest in new[] { true, false })
                {
                    foreach (var item in remaining.ToList())
                    {
                        if (slotItems.Count >= slotCapacity)
                            break;

                        var (home, away) = teamsOf(item);
                        if (playingThisSlot.Contains(home) || playingThisSlot.Contains(away))
                            continue;

                        if (requireRest && (playedPreviousSlot.Contains(home) || playedPreviousSlot.Contains(away)))
                            continue;

                        slotItems.Add(item);
                        remaining.Remove(item);
                        playingThisSlot.Add(home);
                        playingThisSlot.Add(away);
                    }
                }

                for (var i = 0; i < slotItems.Count; i++)
                {
                    placements.Add(new Placement<T>(slotItems[i], freePitches[i].Id, slotStart));
                    occupiedSlots.Add((freePitches[i].Id, slotStart));
                }

                placedInWindow[windowIndex] += slotItems.Count;
                playedPreviousSlot = playingThisSlot;
            }

            return (placements, remaining.Count);
        }

        // Ta sama drużyna z bazy może grać w kilku kategoriach (osobne zgłoszenia). Dla generatora to
        // jedna drużyna: ma wspólnego trenera, a często i zawodników, więc nie gra dwóch meczów naraz.
        private static Func<Guid, Guid> ClubLookup(IReadOnlyList<TournamentTeamDto> tournamentTeams)
        {
            var teamIdByTournamentTeamId = tournamentTeams.ToDictionary(t => t.Id, t => t.TeamId);
            return tournamentTeamId => teamIdByTournamentTeamId.GetValueOrDefault(tournamentTeamId, tournamentTeamId);
        }

        private static Result<GenerateScheduleResponse> NotEnoughTime(int unplaced) =>
            Result.Failure<GenerateScheduleResponse>(Error.Validation(
                "EndAt", $"Not enough time available to schedule all matches — {unplaced} match(es) could not be placed"));

        private static Result<GenerateScheduleResponse> StatusConflict() =>
            Result.Failure<GenerateScheduleResponse>(Error.Conflict("Status", "Tournament cannot transition to ScheduleGenerated from its current status"));

        private async Task<Result<GenerateScheduleResponse>> CompleteAsync(Guid tournamentId, int matchCount, CancellationToken cancellationToken)
        {
            // Zdarzenie dopiero po udanej zmianie etapu — przy wycofaniu nie może trafić do outboxa.
            var scheduleGenerated = new ScheduleGenerated(tournamentId, matchCount, currentUserContext.UserId);
            var outboxMessage = OutboxMessage.Create(
                scheduleGenerated.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(scheduleGenerated, scheduleGenerated.GetType()));
            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new GenerateScheduleResponse(matchCount));
        }

        // Metoda kołowa: n-1 kolejek (n parzyste), w każdej kolejce każda drużyna gra najwyżej raz.
        // Przy nieparzystej liczbie drużyn dokładamy "pauzę" (Guid.Empty) — ta para jest pomijana.
        private static List<List<MatchPair>> BuildRoundRobinRounds(TournamentGroupDto group, List<Guid> teamIds)
        {
            var teams = new List<Guid>(teamIds);
            if (teams.Count % 2 == 1)
                teams.Add(Guid.Empty);

            var rounds = new List<List<MatchPair>>();
            for (var round = 0; round < teams.Count - 1; round++)
            {
                var roundPairs = new List<MatchPair>();
                for (var i = 0; i < teams.Count / 2; i++)
                {
                    var home = teams[i];
                    var away = teams[teams.Count - 1 - i];
                    if (home == Guid.Empty || away == Guid.Empty)
                        continue;

                    // Naprzemiennie gospodarz/gość, żeby pierwsza drużyna nie była zawsze gospodarzem.
                    roundPairs.Add(round % 2 == 0
                        ? new MatchPair(group.CategoryId, group.Id, home, away)
                        : new MatchPair(group.CategoryId, group.Id, away, home));
                }

                rounds.Add(roundPairs);

                // Rotacja: pierwsza drużyna stoi, reszta przesuwa się o jedno miejsce.
                var last = teams[^1];
                teams.RemoveAt(teams.Count - 1);
                teams.Insert(1, last);
            }

            return rounds;
        }
    }
}
