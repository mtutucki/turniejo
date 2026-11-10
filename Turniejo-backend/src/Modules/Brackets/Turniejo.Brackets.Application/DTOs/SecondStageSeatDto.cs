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
}
