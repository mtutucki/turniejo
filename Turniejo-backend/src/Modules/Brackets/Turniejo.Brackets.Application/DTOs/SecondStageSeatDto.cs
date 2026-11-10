using Turniejo.Brackets.Application.Abstractions;

namespace Turniejo.Brackets.Application.DTOs
{
    /// <summary>Miejsce w grupie drugiej fazy: skąd przychodzi drużyna i (po fazie 1) która to jest.</summary>
    public sealed record SecondStageSeatDto(
        Guid Id,
        Guid CategoryId,
        Guid GroupId,
        int Order,
        Guid SourceGroupId,
        int SourcePosition,
        Guid? TournamentTeamId);

    /// <summary>Mecz grupy fazy 2 zaplanowany przez generator. MatchId pojawia się, gdy oba miejsca mają drużyny.</summary>
    public sealed record SecondStageFixtureDto(
        Guid Id,
        Guid CategoryId,
        Guid GroupId,
        Guid HomeSeatId,
        Guid AwaySeatId,
        int Round,
        Guid? PlannedPitchId,
        DateTimeOffset? PlannedAt,
        Guid? MatchId);

    public sealed record SecondStageDto(IReadOnlyList<SecondStageSeatDto> Seats, IReadOnlyList<SecondStageFixtureDto> Fixtures)
    {
        public static async Task<SecondStageDto> LoadAsync(IBracketsDbContext dbContext, Guid tournamentId, CancellationToken cancellationToken)
        {
            var seats = await dbContext.GetSecondStageSeatsAsync(tournamentId, cancellationToken);
            var fixtures = await dbContext.GetSecondStageFixturesAsync(tournamentId, cancellationToken);
            return new SecondStageDto(
                seats.Select(s => new SecondStageSeatDto(s.Id, s.CategoryId, s.GroupId, s.Order, s.SourceGroupId, s.SourcePosition, s.TournamentTeamId)).ToList(),
                fixtures.Select(f => new SecondStageFixtureDto(f.Id, f.CategoryId, f.GroupId, f.HomeSeatId, f.AwaySeatId, f.Round, f.PlannedPitchId, f.PlannedAt, f.MatchId)).ToList());
        }
    }
}
