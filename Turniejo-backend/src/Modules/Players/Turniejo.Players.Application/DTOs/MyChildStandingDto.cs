using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.Application.DTOs
{
    public sealed record MyChildStandingDto(
        Guid TournamentTeamId,
        Guid GroupId,
        IReadOnlyList<GroupStandingDto> Standings,
        // Rodzic zwykle nie jest członkiem turnieju, więc nie pobierze nazw drużyn z innego endpointu.
        string? GroupName = null,
        IReadOnlyDictionary<Guid, string>? TeamNames = null,
        // Dziecko może grać w kilku turniejach — sama „Grupa A” nie mówi, której tabeli dotyczy.
        string? TournamentName = null);
}
