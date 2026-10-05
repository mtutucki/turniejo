using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.Brackets.GetPublicBracket
{
    internal sealed class GetPublicBracketQueryHandler(
        IBracketsDbContext dbContext,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicBracketQuery, BracketDto>
    {
        public async Task<Result<BracketDto>> Handle(GetPublicBracketQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<BracketDto>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowBracket)
                return Result.Failure<BracketDto>(Error.NotFound("Bracket", request.CategoryId));

            var bracket = await dbContext.GetBracketByTournamentAndCategoryAsync(tournament.Id, request.CategoryId, cancellationToken);
            if (bracket == null)
                return Result.Failure<BracketDto>(Error.NotFound("Bracket", request.CategoryId));

            var rounds = await dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, cancellationToken);
            var roundIds = rounds.Select(r => r.Id).ToList();
            var nodes = await dbContext.GetBracketNodesByRoundIdsAsync(roundIds, cancellationToken);
            var nodesByRound = nodes.ToLookup(n => n.BracketRoundId);

            var roundDtos = rounds
                .OrderBy(r => r.RoundNumber)
                .ThenBy(r => r.RoundType)
                .Select(r => new BracketRoundDto(
                    r.Id, r.RoundNumber, r.RoundType.ToString(), r.Name,
                    nodesByRound[r.Id]
                        .Select(n => new BracketNodeDto(
                            n.Id, n.BracketRoundId, n.MatchId, n.HomeSourceType.ToString(), n.HomeTournamentTeamId,
                            n.AwaySourceType.ToString(), n.AwayTournamentTeamId, n.WinnerGoesToNodeId, n.LoserGoesToNodeId,
                            n.WinnerGoesToSlot?.ToString(),
                            n.HomeSourceGroupId, n.HomeSourcePosition, n.AwaySourceGroupId, n.AwaySourcePosition,
                            n.PlannedPitchId, n.PlannedAt))
                        .ToList()))
                .ToList();

            var dto = new BracketDto(bracket.Id, bracket.TournamentId, roundDtos);

            return Result.Success(dto);
        }
    }
}
