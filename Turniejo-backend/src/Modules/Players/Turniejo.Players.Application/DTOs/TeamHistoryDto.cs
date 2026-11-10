namespace Turniejo.Players.Application.DTOs
{
    /// <summary>
    /// Statystyki drużyny z bazy organizacji we wszystkich turniejach (GET /api/teams/{teamId}/history).
    /// Liczą się zaakceptowane zgłoszenia w turniejach, które nie zostały odwołane, i tylko zakończone mecze.
    /// Form: ostatnie wyniki, najstarszy pierwszy: "W", "D", "L" (do 5).
    /// </summary>
    public sealed record TeamHistoryDto(
        TeamRecordDto Summary,
        int TournamentCount,
        IReadOnlyList<string> Form,
        IReadOnlyList<TeamHistoryTournamentDto> Tournaments,
        IReadOnlyList<TeamHistoryPlayerDto> Players,
        IReadOnlyList<TeamHistoryOpponentDto> Opponents);

    public sealed record TeamRecordDto(int Played, int Wins, int Draws, int Losses, int GoalsFor, int GoalsAgainst);

    /// <summary>
    /// Jedno zgłoszenie (turniej + kategoria). ResultKind: Champion, RunnerUp, Third, Fourth, Eliminated
    /// (EliminatedRoundFromEnd: 2 = półfinał, 3 = ćwierćfinał…), InCup (jeszcze gra w pucharze), Group
    /// (miejsce w grupie: GroupPosition + GroupName), None (brak meczów albo wyniku).
    /// </summary>
    public sealed record TeamHistoryTournamentDto(
        Guid TournamentId,
        string TournamentName,
        string TournamentStatus,
        DateTimeOffset? StartAt,
        DateTimeOffset? EndAt,
        Guid TournamentTeamId,
        string? CategoryName,
        string ResultKind,
        int? EliminatedRoundFromEnd,
        int? GroupPosition,
        string? GroupName,
        TeamRecordDto Record);

    public sealed record TeamHistoryPlayerDto(
        Guid PlayerId,
        string FirstName,
        string LastName,
        int Goals,
        int Assists,
        int Mvp,
        int YellowCards,
        int RedCards);

    /// <summary>Bilans z rywalem: ta sama drużyna z bazy przeciwnika we wszystkich turniejach.</summary>
    public sealed record TeamHistoryOpponentDto(Guid TeamId, string Name, TeamRecordDto Record);
}
