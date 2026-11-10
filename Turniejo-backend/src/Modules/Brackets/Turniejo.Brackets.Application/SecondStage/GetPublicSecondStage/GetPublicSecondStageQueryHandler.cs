using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.SecondStage.GetPublicSecondStage
{
    /// <summary>
    /// Portal kibica: podział na grupy drugiej fazy (tabele przed rozstrzygnięciem) i zaplanowane mecze
    /// (terminarz). Bez terminarza i tabel w portalu nie ma czego pokazać.
    /// </summary>
    internal sealed class GetPublicSecondStageQueryHandler(
        IBracketsDbContext dbContext,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicSecondStageQuery, SecondStageDto>
    {
        public async Task<Result<SecondStageDto>> Handle(GetPublicSecondStageQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<SecondStageDto>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowSchedule && !tournament.Settings.ShowStandings)
                return Result.Success(new SecondStageDto([], [], []));

            return Result.Success(await SecondStageDto.LoadAsync(dbContext, tournament.Id, cancellationToken));
        }
    }
}
