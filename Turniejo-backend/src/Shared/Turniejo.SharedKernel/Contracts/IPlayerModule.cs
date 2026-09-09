namespace Turniejo.SharedKernel.Contracts;

public interface IPlayerModule
{
    Task<IReadOnlyList<TournamentTeamPlayerDto>> GetTournamentTeamPlayersAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerDto>> GetPlayersAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);

    // Pozwala innym modułom (np. Matches) sprawdzić dostęp rodzica/opiekuna bez znajomości
    // wewnętrznej struktury PlayerGuardian — moduł Players sam decyduje, kogo uznaje za opiekuna.
    Task<bool> IsGuardianOfAnyPlayerOnTeamsAsync(Guid userId, IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default);

    // Dla modułu Notifications przy rozsyłce "cały turniej" (ogłoszenie organizatora, anulowanie
    // turnieju) — zwraca tylko opiekunów z CanReceiveNotifications=true, więc wywołujący nie musi
    // znać/filtrować tej flagi samodzielnie.
    Task<IReadOnlyList<Guid>> GetNotifiableGuardianUserIdsForPlayersAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);
}

public record PlayerDto(Guid Id, string FirstName, string LastName);

public record TournamentTeamPlayerDto(
    Guid Id,
    Guid TournamentTeamId,
    Guid PlayerId,
    string FirstName,
    string LastName,
    int? ShirtNumber,
    string? Position,
    string Status);
