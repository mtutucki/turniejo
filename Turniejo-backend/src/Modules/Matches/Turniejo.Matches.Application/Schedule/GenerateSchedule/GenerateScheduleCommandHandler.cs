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
    // Jeden terminarz dla całego turnieju, wszystkie kategorie razem (ScheduleBuilder):
    // - grupy: tworzy mecze fazy grupowej (każdy z każdym),
    // - puchar: mecze 1. rundy tworzy drabinka (GenerateBracket), tu dostają termin i boisko,
    // - kolejne rundy (także po fazie grupowej) dostają zaplanowany termin na węźle drabinki; mecz
    //   powstaje w nim po rozstrzygnięciu (CreateBracketMatch).
    // Drabinka musi istnieć w każdej kategorii. Na końcu etap turnieju zmienia się na ScheduleGenerated.
    internal sealed class GenerateScheduleCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        ITeamModule teamModule,
        IBracketModule bracketModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<GenerateScheduleCommand, GenerateScheduleResponse>
    {
        private sealed record MatchPair(Guid CategoryId, Guid GroupId, Guid HomeTournamentTeamId, Guid AwayTournamentTeamId);

        // Mecz grupy fazy 2 przed rozstrzygnięciem: para miejsc (Id miejsc w polach drużyn), czeka na grupy źródłowe obu miejsc.
        private sealed record SecondStagePlan(Guid Key, MatchPair Seats, int Round, IReadOnlySet<Guid> SourceGroupIds);

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

            if (tournament.Format is not ("Group" or "GroupAndKnockout" or "Knockout" or "TwoGroupStages"))
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("Format", "Set the tournament format before generating the schedule"));

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<GenerateScheduleResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            var allPitches = await dbContext.GetPitchesByIdsAsync(request.PitchIds, cancellationToken);
            var pitches = allPitches.Where(p => p.TournamentId == request.TournamentId && p.IsActive).ToList();
            if (pitches.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("PitchIds", "No valid active pitches belonging to this tournament were provided"));

            var existingMatches = await dbContext.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken);
            var tournamentTeams = await teamModule.GetTournamentTeamsByTournamentIdAsync(request.TournamentId, cancellationToken);
            var accepted = tournamentTeams.Where(t => t.RegistrationStatus == "Accepted").ToList();
            var clubOf = ClubLookup(tournamentTeams);

            var withGroups = tournament.Format is "Group" or "GroupAndKnockout" or "TwoGroupStages";
            var withBracket = tournament.Format is "Knockout" or "GroupAndKnockout";

            var pairs = new List<(Guid Key, MatchPair Pair, int Round)>();
            if (withGroups)
            {
                var groupResult = await BuildGroupPairsAsync(request.TournamentId, accepted, existingMatches, cancellationToken);
                if (groupResult.IsFailure)
                    return Result.Failure<GenerateScheduleResponse>(groupResult.Error);
                pairs = groupResult.Value;
            }

            var secondStage = new List<SecondStagePlan>();
            if (tournament.Format == "TwoGroupStages")
            {
                var secondStageResult = await BuildSecondStageAsync(request.TournamentId, accepted, cancellationToken);
                if (secondStageResult.IsFailure)
                    return Result.Failure<GenerateScheduleResponse>(secondStageResult.Error);
                secondStage = secondStageResult.Value;
            }

            IReadOnlyList<BracketNodeInfoDto> nodes = withBracket
                ? await bracketModule.GetNodesByTournamentIdAsync(request.TournamentId, cancellationToken)
                : [];

            // Mecze pucharu, które już istnieją i się nie zaczęły (1. runda, a w grupach + pucharze pary
            // ustawione ręcznie). Dostają nowy termin; reszta istniejących meczów zostaje tam, gdzie jest.
            var bracketMatchIds = withBracket
                ? existingMatches
                    .Where(m => BracketStages.Contains(m.StageType) &&
                                m.Status is MatchStatus.Scheduled or MatchStatus.Ready or MatchStatus.Postponed)
                    .Select(m => m.Id)
                    .ToHashSet()
                : [];
            if (tournament.Format == "Knockout" && bracketMatchIds.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation(
                    "Bracket", "Generate the knockout bracket first — the schedule places its first-round matches"));

            // Kategoria z jedną drużyną nie ma meczów, a terminarz generuje się raz dla całego turnieju.
            if (accepted.GroupBy(t => t.CategoryId).Any(g => g.Count() == 1))
                return Result.Failure<GenerateScheduleResponse>(Error.Validation(
                    "TournamentId", "Each category needs at least 2 accepted teams"));

            // Każda kategoria potrzebuje drabinki. Bez tego generator rozstawiał tylko gotowe drabinki,
            // etap zmieniał się na "harmonogram gotowy", a pozostała kategoria zostawała bez meczów.
            if (withBracket)
            {
                var categoriesWithBracket = nodes.Select(n => n.CategoryId).ToHashSet();
                if (accepted.Select(t => t.CategoryId).Distinct().Any(c => !categoriesWithBracket.Contains(c)))
                    return Result.Failure<GenerateScheduleResponse>(Error.Validation(
                        "Bracket", "Every category needs a knockout bracket before generating the schedule"));
            }

            // existingMatches jest tylko do odczytu (AsNoTracking) — zmiany na nich nie trafiały do bazy.
            var bracketMatches = bracketMatchIds.Count > 0
                ? await dbContext.GetTrackedMatchesByIdsAsync(bracketMatchIds, cancellationToken)
                : [];

            var items = BuildItems(pairs, bracketMatches, nodes, accepted, clubOf);
            items.AddRange(SecondStageItems(secondStage, pairs, accepted, clubOf));

            var fixedMatches = existingMatches
                .Where(m => m.Status != MatchStatus.Cancelled && m.PitchId.HasValue && !bracketMatchIds.Contains(m.Id))
                .Select(m => new FixedMatch(m.PitchId, m.ScheduledAt,
                    new HashSet<Guid> { clubOf(m.HomeTournamentTeamId), clubOf(m.AwayTournamentTeamId) }))
                .ToList();

            var result = ScheduleBuilder.Build(items, pitches.Select(p => p.Id).ToList(), fixedMatches, windows, settings);
            if (result.Unplaced > 0)
                return NotEnoughTime(result.Unplaced);

            var newMatches = new List<Match>();
            foreach (var (key, pair, _) in pairs)
            {
                var slot = result.Slots[key];
                var matchResult = Match.Create(
                    Guid.NewGuid(), request.TournamentId, pair.CategoryId, pair.GroupId,
                    pair.HomeTournamentTeamId, pair.AwayTournamentTeamId, slot.PitchId, slot.StartAt, StageType.Group);
                if (matchResult.IsFailure)
                    return Result.Failure<GenerateScheduleResponse>(matchResult.Error);
                newMatches.Add(matchResult.Value);
            }

            var plannedSlots = nodes
                .Where(n => !n.MatchId.HasValue && result.Slots.ContainsKey(n.Id))
                .Select(n => new BracketPlannedSlotDto(n.Id, result.Slots[n.Id].PitchId, result.Slots[n.Id].StartAt))
                .ToList();

            // Mecze i etap turnieju żyją w dwóch bazach modułów, więc nie ma jednej transakcji. Kolejność:
            // najpierw mecze, potem etap — a gdy zmiana etapu się nie uda, cofamy właśnie zapisane mecze.
            // Odwrotnie (etap pierwszy) błąd zapisu meczów zostawiał "harmonogram gotowy" z zerem meczów.
            if (newMatches.Count > 0)
            {
                await dbContext.AddMatchesAsync(newMatches, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var previous = bracketMatches.ToDictionary(m => m.Id, m => (m.PitchId, m.ScheduledAt));
            // Bez MatchScheduleChanged w outboxie: terminarz dopiero powstaje, to nie "zmiana terminu"
            // do rozesłania rodzicom (tak samo nowe mecze fazy grupowej nie wysyłają powiadomień).
            if (bracketMatches.Count > 0)
                await ApplySlotsAsync(
                    bracketMatches.Select(m => (m, (Guid?)result.Slots[m.Id].PitchId, result.Slots[m.Id].StartAt)).ToList(),
                    cancellationToken);

            if (!await tournamentModule.ChangeTournamentStatusAsync(request.TournamentId, "ScheduleGenerated", cancellationToken))
            {
                if (bracketMatches.Count > 0)
                    await ApplySlotsAsync(
                        bracketMatches.Select(m => (m, previous[m.Id].PitchId, previous[m.Id].ScheduledAt)).ToList(),
                        cancellationToken);
                if (newMatches.Count > 0)
                {
                    await dbContext.RemoveMatchesAsync(newMatches, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return StatusConflict();
            }

            if (plannedSlots.Count > 0)
                await bracketModule.SetPlannedSlotsAsync(plannedSlots, cancellationToken);

            if (secondStage.Count > 0)
                await bracketModule.CreateSecondStageFixturesAsync(request.TournamentId, secondStage
                    .Select(f => new SecondStageFixturePlanDto(
                        f.Key, f.Seats.CategoryId, f.Seats.GroupId, f.Seats.HomeTournamentTeamId, f.Seats.AwayTournamentTeamId,
                        f.Round + 1, result.Slots[f.Key].PitchId, result.Slots[f.Key].StartAt))
                    .ToList(), cancellationToken);

            return await CompleteAsync(
                request.TournamentId, tournament.Format == "Knockout" ? bracketMatches.Count : newMatches.Count, cancellationToken);
        }

        private async Task<Result<List<(Guid Key, MatchPair Pair, int Round)>>> BuildGroupPairsAsync(
            Guid tournamentId,
            List<TournamentTeamDto> accepted,
            IReadOnlyList<Match> existingMatches,
            CancellationToken cancellationToken)
        {
            var groups = await tournamentModule.GetTournamentGroupsAsync(tournamentId, cancellationToken);
            if (groups.Count == 0)
                return Result.Failure<List<(Guid, MatchPair, int)>>(Error.Validation("TournamentId", "Tournament has no groups defined"));

            // Mecze grają tylko drużyny przypisane do grupy. Zaakceptowana drużyna bez grupy zostałaby po
            // cichu pominięta, a terminarz generuje się raz — organizator zauważał to dopiero po fakcie.
            if (accepted.Any(t => !t.GroupId.HasValue))
                return Result.Failure<List<(Guid, MatchPair, int)>>(Error.Validation("TournamentId", "Some accepted teams are not assigned to a group"));

            var teamsByGroup = accepted
                .GroupBy(t => t.GroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());

            var pairs = new List<(Guid Key, MatchPair Pair, int Round)>();
            // Grupy drugiej fazy nie mają przydzielonych drużyn, ich mecze planuje BuildSecondStageAsync.
            foreach (var group in groups.Where(g => g.Stage == 1))
            {
                if (!teamsByGroup.TryGetValue(group.Id, out var teamIds) || teamIds.Count < 2)
                    continue;

                var rounds = BuildRoundRobinRounds(group, teamIds);
                for (var round = 0; round < rounds.Count; round++)
                    pairs.AddRange(rounds[round].Select(pair => (Guid.NewGuid(), pair, round)));
            }

            if (pairs.Count == 0)
                return Result.Failure<List<(Guid, MatchPair, int)>>(Error.Validation(
                    "TournamentId", "No matches to schedule — each group needs at least 2 accepted teams"));

            // Grupa z jedną drużyną była pomijana po cichu, a ta drużyna nie grała ani razu.
            if (teamsByGroup.Values.Any(teams => teams.Count == 1))
                return Result.Failure<List<(Guid, MatchPair, int)>>(Error.Validation("TournamentId", "Each group needs at least 2 accepted teams"));

            // Pozostałość po nieudanym generowaniu (mecze zapisane, etap nie zmieniony) — ponowne generowanie
            // zdublowałoby pary. Organizator czyści terminarz (ClearSchedule) i generuje od nowa.
            if (existingMatches.Any(m => m.StageType == StageType.Group && m.Status != MatchStatus.Cancelled))
                return Result.Failure<List<(Guid, MatchPair, int)>>(Error.Validation(
                    "TournamentId", "Tournament already has group matches — clear the schedule first"));

            // Kolejka 1 wszystkich grup, potem kolejka 2 itd. — kolejność bazowa dla układacza.
            return Result.Success(pairs.OrderBy(p => p.Round).ToList());
        }

        /// <summary>
        /// Format „Dwie fazy grupowe”: każdy z każdym w grupach fazy 2, ale między miejscami („1. miejsce Gr. A”),
        /// bo drużyny będą znane dopiero po fazie 1. Każda kategoria potrzebuje grup fazy 2 z co najmniej
        /// dwoma miejscami, a miejsce musi istnieć w tabeli grupy źródłowej.
        /// </summary>
        private async Task<Result<List<SecondStagePlan>>> BuildSecondStageAsync(
            Guid tournamentId, List<TournamentTeamDto> accepted, CancellationToken cancellationToken)
        {
            var groups = await tournamentModule.GetTournamentGroupsAsync(tournamentId, cancellationToken);
            var teamCount = accepted.Where(t => t.GroupId.HasValue).GroupBy(t => t.GroupId!.Value).ToDictionary(g => g.Key, g => g.Count());
            // Zakresy miejsc (albo gotowy podział) zamieniają się teraz w miejsca z prawdziwej liczby drużyn.
            await bracketModule.PrepareSecondStageSeatsAsync(tournamentId, groups, teamCount, cancellationToken);
            var seats = await bracketModule.GetSecondStageSeatsAsync(tournamentId, cancellationToken);
            var seatsByGroup = seats.GroupBy(s => s.GroupId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList());
            var secondStageGroups = groups.Where(g => g.Stage == 2).ToList();

            if (accepted.Select(t => t.CategoryId).Distinct()
                .Any(c => !secondStageGroups.Any(g => g.CategoryId == c && seatsByGroup.ContainsKey(g.Id))))
                return Result.Failure<List<SecondStagePlan>>(Error.Validation(
                    "SecondStage", "Every category needs second-stage groups with places before generating the schedule"));

            if (secondStageGroups.Any(g => seatsByGroup.TryGetValue(g.Id, out var groupSeats) && groupSeats.Count < 2))
                return Result.Failure<List<SecondStagePlan>>(Error.Validation(
                    "SecondStage", "Each second-stage group needs at least 2 places"));

            if (seats.Any(s => s.SourcePosition > teamCount.GetValueOrDefault(s.SourceGroupId)))
                return Result.Failure<List<SecondStagePlan>>(Error.Validation(
                    "SecondStage", "A second-stage place points to a position its group does not have"));

            var sourceBySeat = seats.ToDictionary(s => s.Id, s => s.SourceGroupId);
            var plans = new List<SecondStagePlan>();
            foreach (var group in secondStageGroups)
            {
                if (!seatsByGroup.TryGetValue(group.Id, out var groupSeats))
                    continue;

                var rounds = BuildRoundRobinRounds(group, groupSeats.Select(x => x.Id).ToList());
                for (var round = 0; round < rounds.Count; round++)
                    plans.AddRange(rounds[round].Select(pair => new SecondStagePlan(
                        Guid.NewGuid(), pair, round,
                        new HashSet<Guid> { sourceBySeat[pair.HomeTournamentTeamId], sourceBySeat[pair.AwayTournamentTeamId] })));
            }

            return Result.Success(plans.OrderBy(p => p.Round).ToList());
        }

        /// <summary>
        /// Mecz fazy 2 czeka na wszystkie mecze grup źródłowych obu miejsc i może w nim zagrać każda drużyna
        /// z tych grup (ta sama drużyna z bazy w innej kategorii nie gra wtedy innego meczu, jak w pucharze).
        /// </summary>
        private static IEnumerable<ScheduleItem> SecondStageItems(
            List<SecondStagePlan> plans,
            List<(Guid Key, MatchPair Pair, int Round)> pairs,
            List<TournamentTeamDto> accepted,
            Func<Guid, Guid> clubOf)
        {
            var groupRounds = pairs.Count == 0 ? 0 : pairs.Max(p => p.Round) + 1;
            var pairKeysByGroup = pairs.GroupBy(p => p.Pair.GroupId).ToDictionary(g => g.Key, g => g.Select(p => p.Key).ToList());
            var clubsByGroup = accepted.Where(t => t.GroupId.HasValue)
                .GroupBy(t => t.GroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(t => clubOf(t.Id)).ToHashSet());

            foreach (var plan in plans)
            {
                var possible = plan.SourceGroupIds.SelectMany(g => clubsByGroup.GetValueOrDefault(g) ?? []).ToHashSet();
                var after = plan.SourceGroupIds.SelectMany(g => pairKeysByGroup.GetValueOrDefault(g) ?? []).ToList();
                yield return new ScheduleItem(plan.Key, plan.Seats.CategoryId, new HashSet<Guid>(), possible, after, groupRounds + plan.Round);
            }
        }

        /// <summary>
        /// Mecze grupowe, istniejące mecze pucharu i węzły drabinki bez meczu w jednej liście dla układacza.
        /// Węzeł bez meczu czeka na mecze, z których przyjdą drużyny (albo na fazę grupową swoich grup);
        /// finał czeka też na mecz o 3. miejsce z tych samych półfinałów (na dwóch boiskach grały równolegle).
        /// Przy wolnych losach mecz 2. rundy powstaje od razu, więc też czeka na mecze 1. rundy.
        /// </summary>
        private static List<ScheduleItem> BuildItems(
            List<(Guid Key, MatchPair Pair, int Round)> pairs,
            IReadOnlyList<Match> bracketMatches,
            IReadOnlyList<BracketNodeInfoDto> nodes,
            List<TournamentTeamDto> accepted,
            Func<Guid, Guid> clubOf)
        {
            var items = new List<ScheduleItem>();
            foreach (var (key, pair, round) in pairs)
            {
                var clubs = new HashSet<Guid> { clubOf(pair.HomeTournamentTeamId), clubOf(pair.AwayTournamentTeamId) };
                items.Add(new ScheduleItem(key, pair.CategoryId, clubs, clubs, [], round));
            }

            var groupRounds = pairs.Count == 0 ? 0 : pairs.Max(p => p.Round) + 1;
            var pairKeysByGroup = pairs.GroupBy(p => p.Pair.GroupId).ToDictionary(g => g.Key, g => g.Select(p => p.Key).ToList());
            var clubsByGroup = accepted.Where(t => t.GroupId.HasValue)
                .GroupBy(t => t.GroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(t => clubOf(t.Id)).ToHashSet());
            var nodeById = nodes.ToDictionary(n => n.Id);
            var bracketMatchById = bracketMatches.ToDictionary(m => m.Id);

            // Klucz węzła u układacza: mecz, który dostaje termin, albo sam węzeł (termin planowany).
            // Węzeł z wolnym losem i mecz, który już się odbył, nie mają czego układać.
            Guid? KeyOf(BracketNodeInfoDto node) =>
                node.MatchId is { } matchId
                    ? bracketMatchById.ContainsKey(matchId) ? matchId : (Guid?)null
                    : node.HasBye ? null : node.Id;

            HashSet<Guid> KnownClubs(BracketNodeInfoDto node)
            {
                var known = new HashSet<Guid>();
                if (node.MatchId is { } matchId && bracketMatchById.TryGetValue(matchId, out var match))
                {
                    known.Add(clubOf(match.HomeTournamentTeamId));
                    known.Add(clubOf(match.AwayTournamentTeamId));
                    return known;
                }

                if (node.HomeTournamentTeamId is { } home)
                    known.Add(clubOf(home));
                if (node.AwayTournamentTeamId is { } away)
                    known.Add(clubOf(away));
                return known;
            }

            var possibleMemo = new Dictionary<Guid, HashSet<Guid>>();
            HashSet<Guid> PossibleClubs(BracketNodeInfoDto node)
            {
                if (possibleMemo.TryGetValue(node.Id, out var memo))
                    return memo;

                var result = KnownClubs(node);
                if (!(node.MatchId is { } matchId && bracketMatchById.ContainsKey(matchId)))
                {
                    foreach (var (team, group, source) in new[]
                             {
                                 (node.HomeTournamentTeamId, node.HomeSourceGroupId, node.HomeSourceNodeId),
                                 (node.AwayTournamentTeamId, node.AwaySourceGroupId, node.AwaySourceNodeId),
                             })
                    {
                        if (team.HasValue)
                            continue;
                        if (group is { } groupId && clubsByGroup.TryGetValue(groupId, out var groupClubs))
                            result.UnionWith(groupClubs);
                        else if (source is { } sourceId && nodeById.TryGetValue(sourceId, out var sourceNode))
                            result.UnionWith(PossibleClubs(sourceNode));
                    }
                }

                possibleMemo[node.Id] = result;
                return result;
            }

            // Na co czeka węzeł: mecze, z których przyjdą drużyny, i fazę grupową grup źródłowych. Węzeł
            // z wolnym losem nie ma meczu — wtedy czekamy na to, na co czekałby on sam (np. jego grupę).
            List<Guid> SourcesOf(BracketNodeInfoDto node)
            {
                var keys = new List<Guid>();
                foreach (var groupId in new[] { node.HomeSourceGroupId, node.AwaySourceGroupId })
                {
                    if (groupId is { } id && pairKeysByGroup.TryGetValue(id, out var groupKeys))
                        keys.AddRange(groupKeys);
                }

                foreach (var sourceId in new[] { node.HomeSourceNodeId, node.AwaySourceNodeId })
                {
                    if (sourceId is not { } id || !nodeById.TryGetValue(id, out var source))
                        continue;
                    if (KeyOf(source) is { } sourceKey)
                        keys.Add(sourceKey);
                    else if (source.HasBye)
                        keys.AddRange(SourcesOf(source));
                }

                return keys;
            }

            // Mecz o 3. miejsce ma numer rundy finału — idzie przed finałem.
            foreach (var node in nodes.OrderBy(n => n.RoundNumber).ThenByDescending(n => n.IsThirdPlace))
            {
                if (KeyOf(node) is not { } key)
                    continue;

                var after = SourcesOf(node);

                if (!node.IsThirdPlace)
                {
                    var thirdPlace = nodes.FirstOrDefault(n => n.IsThirdPlace &&
                        n.HomeSourceNodeId == node.HomeSourceNodeId && n.AwaySourceNodeId == node.AwaySourceNodeId);
                    if (thirdPlace != null && KeyOf(thirdPlace) is { } thirdKey)
                        after.Add(thirdKey);
                }

                var categoryId = node.MatchId is { } matchId && bracketMatchById.TryGetValue(matchId, out var match)
                    ? match.CategoryId ?? node.CategoryId
                    : node.CategoryId;
                items.Add(new ScheduleItem(key, categoryId, KnownClubs(node), PossibleClubs(node), after, groupRounds + node.RoundNumber));
            }

            // Mecz pucharu bez węzła drabinki też musi dostać termin; kolejność jak przy tworzeniu.
            var withNode = nodes.Where(n => n.MatchId.HasValue).Select(n => n.MatchId!.Value).ToHashSet();
            foreach (var match in bracketMatches.Where(m => !withNode.Contains(m.Id)).OrderBy(m => m.ScheduledAt).ThenBy(m => m.CreatedAt))
            {
                var clubs = new HashSet<Guid> { clubOf(match.HomeTournamentTeamId), clubOf(match.AwayTournamentTeamId) };
                items.Add(new ScheduleItem(match.Id, match.CategoryId ?? Guid.Empty, clubs, clubs, [], groupRounds + 1));
            }

            return items;
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
