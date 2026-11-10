using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.DTOs;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.SecondStage.GetSecondStageSeats
{
    internal sealed class GetSecondStageSeatsQueryHandler(IBracketsDbContext dbContext)
        : IQueryHandler<GetSecondStageSeatsQuery, IReadOnlyList<SecondStageSeatDto>>
    {
        public async Task<Result<IReadOnlyList<SecondStageSeatDto>>> Handle(GetSecondStageSeatsQuery request, CancellationToken cancellationToken)
        {
            var seats = await dbContext.GetSecondStageSeatsAsync(request.TournamentId, cancellationToken);
            return Result.Success<IReadOnlyList<SecondStageSeatDto>>(seats.Select(ToDto).ToList());
        }

        internal static SecondStageSeatDto ToDto(SecondStageSeat s) =>
            new(s.Id, s.CategoryId, s.GroupId, s.Order, s.SourceGroupId, s.SourcePosition, s.TournamentTeamId);
    }
}
