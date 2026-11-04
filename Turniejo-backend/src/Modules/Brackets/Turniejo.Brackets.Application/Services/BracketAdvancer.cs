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

            // Węzeł, dla którego mecz już powstał, ma zamrożone drużyny — Match.HomeTournamentTeamId/
            // AwayTournamentTeamId są niemutowalne po utworzeniu (Match.cs), więc ciche nadpisanie samego
            // węzła zostawiłoby węzeł i już istniejący mecz w sprzecznych stanach. Dotyczy to głównie
            // korekty wyniku poprzedniego meczu, która zmieniła zwycięzcę PO tym, jak kolejny mecz już
            // powstał — to wymaga ręcznej interwencji, nie automatycznej podmianki.
            if (targetNode.MatchId.HasValue && currentTeamId.HasValue && currentTeamId != tournamentTeamId)
                return false;

            var resolveResult = targetNode.ResolveSlot(slot, tournamentTeamId);
            if (resolveResult.IsFailure)
                return false;

            var stageType = await ResolveStageTypeAsync(targetNode, cancellationToken);

            await TryCreateMatchAsync(targetNode, tournamentId, categoryId, stageType, cancellationToken);

            return true;
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
