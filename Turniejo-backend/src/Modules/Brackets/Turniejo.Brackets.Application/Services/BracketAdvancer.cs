using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.Services
{
    public sealed class BracketAdvancer(
        IBracketsDbContext dbContext,
        IMatchModule matchModule,
        IStandingModule standingModule)
    {
        // Wpycha drużynę w konkretny slot węzła docelowego — po realnym meczu (AdvanceBracketWinnerCommand),
        // po walkowerze-Bye przy generowaniu drabinki (GenerateBracketCommand), albo po rozwiązaniu
        // GroupPosition ze zaktualizowanej tabeli grupowej (ResolveGroupPositionSlotsCommand). Trzy różne
        // wyzwalacze, ten sam mechanizm awansu — stąd wspólne miejsce zamiast trzech kopii tej logiki.
        public async Task<bool> AdvanceIntoAsync(
            Guid targetNodeId, BracketSlot slot, Guid tournamentTeamId,
            Guid tournamentId, Guid categoryId, CancellationToken cancellationToken)
        {
            var targetNode = await dbContext.GetBracketNodeByIdAsync(targetNodeId, cancellationToken);
            if (targetNode == null)
                return false;

            var currentTeamId = slot == BracketSlot.Home ? targetNode.HomeTournamentTeamId : targetNode.AwayTournamentTeamId;

            // Mecz kolejnej rundy już powstał, a poprawka wyniku zmieniła zwycięzcę poprzedniego meczu.
            // Dopóki ten mecz się nie zaczął, podmieniamy w nim drużynę (termin, boisko i sędzia zostają).
            // Po starcie ma już zdarzenia i wynik, więc drużyny zostają, a organizator musi to rozwiązać sam.
            if (targetNode.MatchId is { } existingMatchId && currentTeamId.HasValue && currentTeamId != tournamentTeamId)
            {
                var home = slot == BracketSlot.Home ? tournamentTeamId : targetNode.HomeTournamentTeamId;
                var away = slot == BracketSlot.Away ? tournamentTeamId : targetNode.AwayTournamentTeamId;
                if (home is not { } homeId || away is not { } awayId ||
                    !await matchModule.ChangeUnstartedMatchTeamsAsync(existingMatchId, homeId, awayId, cancellationToken))
                    return false;

                return targetNode.ResolveSlot(slot, tournamentTeamId).IsSuccess;
            }

            var resolveResult = targetNode.ResolveSlot(slot, tournamentTeamId);
            if (resolveResult.IsFailure)
                return false;

            var stageType = await ResolveStageTypeAsync(targetNode, cancellationToken);

            await TryCreateMatchAsync(targetNode, tournamentId, categoryId, stageType, cancellationToken);

            return true;
        }

        // Zwycięzca idzie dalej, a w półfinale przegrany trafia do meczu o 3. miejsce. Wołane po meczu,
        // po korekcie jego wyniku i przy ręcznym przeliczeniu drabinki przez organizatora.
        public async Task<bool> AdvanceFromNodeAsync(
            BracketNode node, Guid winnerId, Guid tournamentId, Guid categoryId, CancellationToken cancellationToken)
        {
            Guid? loserId = null;
            if (node.HomeTournamentTeamId == winnerId)
                loserId = node.AwayTournamentTeamId;
            else if (node.AwayTournamentTeamId == winnerId)
                loserId = node.HomeTournamentTeamId;

            var advanced = false;

            if (node.WinnerGoesToNodeId.HasValue && node.WinnerGoesToSlot.HasValue)
            {
                advanced |= await AdvanceIntoAsync(
                    node.WinnerGoesToNodeId.Value, node.WinnerGoesToSlot.Value, winnerId, tournamentId, categoryId, cancellationToken);
            }

            if (node.LoserGoesToNodeId.HasValue && node.LoserGoesToSlot.HasValue && loserId.HasValue)
            {
                advanced |= await AdvanceIntoAsync(
                    node.LoserGoesToNodeId.Value, node.LoserGoesToSlot.Value, loserId.Value, tournamentId, categoryId, cancellationToken);
            }

            return advanced;
        }

        public async Task<string> ResolveStageTypeAsync(BracketNode node, CancellationToken cancellationToken)
        {
            // Węzeł o 3. miejsce NIGDY nie ma WinnerGoesToNodeId (zwycięzca nikąd dalej nie awansuje) —
            // dokładnie tak samo jak węzeł Finału. Samo sprawdzenie "WinnerGoesToNodeId == null" nie
            // odróżnia tych dwóch przypadków, więc RoundType musi być sprawdzony najpierw i
            // bezwarunkowo, inaczej mecz o 3. miejsce dostawał StageType "Final".
            var round = await dbContext.GetBracketRoundByIdAsync(node.BracketRoundId, cancellationToken);
            if (round?.RoundType == BracketRoundType.ThirdPlace)
                return "ThirdPlace";

            return node.WinnerGoesToNodeId is null ? "Final" : "Knockout";
        }

        // Węzeł z Bye z jednej strony nigdy nie dostaje meczu (IsFullyResolved zawsze false), więc
        // MatchFinished nigdy się dla niego nie zdarzy — jak tylko druga strona ma drużynę (od razu przy
        // Fixed, albo później przy GroupPosition po aktualizacji tabeli), trzeba ją ręcznie wepchnąć dalej.
        public async Task<bool> TryAdvanceByeAsync(
            BracketNode node, Guid tournamentId, Guid categoryId, CancellationToken cancellationToken)
        {
            var isByeNode = (node.HomeSourceType == BracketSourceType.Bye) != (node.AwaySourceType == BracketSourceType.Bye);
            if (!isByeNode)
                return false;

            var advancingTeamId = node.HomeSourceType == BracketSourceType.Bye
                ? node.AwayTournamentTeamId
                : node.HomeTournamentTeamId;

            if (!advancingTeamId.HasValue || !node.WinnerGoesToNodeId.HasValue || !node.WinnerGoesToSlot.HasValue)
                return false;

            return await AdvanceIntoAsync(
                node.WinnerGoesToNodeId.Value, node.WinnerGoesToSlot.Value, advancingTeamId.Value,
                tournamentId, categoryId, cancellationToken);
        }

        public async Task ResolveGroupPositionSourcesAsync(BracketNode node, CancellationToken cancellationToken)
        {
            if (node.HomeSourceType == BracketSourceType.GroupPosition && !node.HomeTournamentTeamId.HasValue)
            {
                var teamId = await ResolveGroupPositionAsync(node.HomeSourceGroupId!.Value, node.HomeSourcePosition!.Value, cancellationToken);
                if (teamId.HasValue)
                    node.ResolveSlot(BracketSlot.Home, teamId.Value);
            }

            if (node.AwaySourceType == BracketSourceType.GroupPosition && !node.AwayTournamentTeamId.HasValue)
            {
                var teamId = await ResolveGroupPositionAsync(node.AwaySourceGroupId!.Value, node.AwaySourcePosition!.Value, cancellationToken);
                if (teamId.HasValue)
                    node.ResolveSlot(BracketSlot.Away, teamId.Value);
            }
        }

        /// <summary>
        /// Korekta wyniku grupowego po zakończeniu fazy grupowej zmienia tabelę, a obsadzony już slot
        /// z miejsca w grupie zostawał ze starą drużyną. Przelicza obsadzone sloty z danej grupy:
        /// nierozpoczęty mecz dostaje nową drużynę, rozpoczęty zostaje i trafia do <paramref name="blockedMatchIds"/>.
        /// </summary>
        public async Task<bool> RefreshResolvedGroupSlotsAsync(
            BracketNode node, Guid groupId, Guid tournamentId, Guid categoryId,
            ICollection<Guid> blockedMatchIds, CancellationToken cancellationToken)
        {
            var changed = false;
            foreach (var slot in new[] { BracketSlot.Home, BracketSlot.Away })
            {
                var home = slot == BracketSlot.Home;
                var sourceType = home ? node.HomeSourceType : node.AwaySourceType;
                var sourceGroup = home ? node.HomeSourceGroupId : node.AwaySourceGroupId;
                var position = home ? node.HomeSourcePosition : node.AwaySourcePosition;
                var current = home ? node.HomeTournamentTeamId : node.AwayTournamentTeamId;

                if (sourceType != BracketSourceType.GroupPosition || sourceGroup != groupId || current is null || position is null)
                    continue;

                var expected = await ResolveGroupPositionAsync(groupId, position.Value, cancellationToken);
                if (expected is not { } expectedId || expectedId == current)
                    continue;

                if (node.MatchId is { } matchId)
                {
                    var newHome = home ? expectedId : node.HomeTournamentTeamId!.Value;
                    var newAway = home ? node.AwayTournamentTeamId!.Value : expectedId;
                    if (!await matchModule.ChangeUnstartedMatchTeamsAsync(matchId, newHome, newAway, cancellationToken))
                    {
                        blockedMatchIds.Add(matchId);
                        continue;
                    }
                }

                node.ResolveSlot(slot, expectedId);
                changed = true;

                // Para z wolnym losem nie ma meczu - drużyna z grupy przeszła już dalej, więc poprawiamy ją tam.
                var otherIsBye = (home ? node.AwaySourceType : node.HomeSourceType) == BracketSourceType.Bye;
                if (otherIsBye && node.WinnerGoesToNodeId is { } nextNodeId && node.WinnerGoesToSlot is { } nextSlot)
                    await AdvanceIntoAsync(nextNodeId, nextSlot, expectedId, tournamentId, categoryId, cancellationToken);
            }

            return changed;
        }

        public async Task<Result> TryCreateMatchAsync(
            BracketNode node, Guid tournamentId, Guid categoryId, string stageType, CancellationToken cancellationToken)
        {
            if (!node.IsFullyResolved || node.MatchId.HasValue)
                return Result.Success();

            var matchId = await matchModule.CreateBracketMatchAsync(
                tournamentId, categoryId, node.HomeTournamentTeamId!.Value, node.AwayTournamentTeamId!.Value, stageType,
                node.PlannedPitchId, node.PlannedAt, node.PlannedRefereeId, cancellationToken);

            return node.AssignMatch(matchId);
        }

        private async Task<Guid?> ResolveGroupPositionAsync(Guid groupId, int position, CancellationToken cancellationToken)
        {
            // Tabela zmienia się po każdym meczu, a obsadzony slot już się nie przelicza — miejsce z tabeli
            // w trakcie fazy grupowej wpisywało do pucharu drużyny, które ostatecznie zajęły inne miejsca.
            if (!await matchModule.IsGroupStageFinishedAsync(groupId, cancellationToken))
                return null;

            var standings = await standingModule.GetGroupStandingsAsync(groupId, cancellationToken);

            return standings.FirstOrDefault(s => s.Position == position)?.TournamentTeamId;
        }
    }
}
