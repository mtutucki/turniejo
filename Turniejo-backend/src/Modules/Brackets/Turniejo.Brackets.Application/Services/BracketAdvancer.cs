using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.Services
{
    public sealed class BracketAdvancer(IMatchModule matchModule, IStandingModule standingModule)
    {
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
                tournamentId, categoryId, node.HomeTournamentTeamId!.Value, node.AwayTournamentTeamId!.Value, stageType, cancellationToken);

            return node.AssignMatch(matchId);
        }

        private async Task<Guid?> ResolveGroupPositionAsync(Guid groupId, int position, CancellationToken cancellationToken)
        {
            var standings = await standingModule.GetGroupStandingsAsync(groupId, cancellationToken);

            return standings.FirstOrDefault(s => s.Position == position)?.TournamentTeamId;
        }
    }
}
