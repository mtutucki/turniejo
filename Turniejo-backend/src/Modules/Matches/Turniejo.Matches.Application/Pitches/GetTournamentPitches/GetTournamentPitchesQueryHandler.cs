using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Pitches.GetTournamentPitches
{
    internal sealed class GetTournamentPitchesQueryHandler(IMatchesDbContext dbContext) : IQueryHandler<GetTournamentPitchesQuery, IReadOnlyList<PitchDto>>
    {
        public async Task<Result<IReadOnlyList<PitchDto>>> Handle(GetTournamentPitchesQuery request, CancellationToken cancellationToken)
        {
            var pitches = await dbContext.GetPitchesByTournamentIdAsync(request.TournamentId, cancellationToken);

            var dtos = pitches.Select(p => new PitchDto(p.Id, p.TournamentId, p.Name, p.Description, p.IsActive)).ToList();

            return Result.Success<IReadOnlyList<PitchDto>>(dtos);
        }
    }
}
