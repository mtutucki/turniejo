using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.CreateBracketMatch
{
    internal sealed class CreateBracketMatchCommandHandler(IMatchesDbContext dbContext)
        : ICommandHandler<CreateBracketMatchCommand, CreateBracketMatchResponse>
    {
        public async Task<Result<CreateBracketMatchResponse>> Handle(CreateBracketMatchCommand request, CancellationToken cancellationToken)
        {
            var result = Match.Create(
                Guid.NewGuid(), request.TournamentId, request.CategoryId, null,
                request.HomeTournamentTeamId, request.AwayTournamentTeamId, null,
                DateTimeOffset.UtcNow, request.StageType);
            if (result.IsFailure)
                return Result.Failure<CreateBracketMatchResponse>(result.Error);

            await dbContext.AddMatchAsync(result.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateBracketMatchResponse(result.Value.Id));
        }
    }
}
