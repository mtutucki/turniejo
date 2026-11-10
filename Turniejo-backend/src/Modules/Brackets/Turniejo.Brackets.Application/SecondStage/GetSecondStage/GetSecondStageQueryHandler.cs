using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.SecondStage.GetSecondStage
{
    internal sealed class GetSecondStageQueryHandler(IBracketsDbContext dbContext)
        : IQueryHandler<GetSecondStageQuery, SecondStageDto>
    {
        public async Task<Result<SecondStageDto>> Handle(GetSecondStageQuery request, CancellationToken cancellationToken) =>
            Result.Success(await SecondStageDto.LoadAsync(dbContext, request.TournamentId, cancellationToken));
    }
}
