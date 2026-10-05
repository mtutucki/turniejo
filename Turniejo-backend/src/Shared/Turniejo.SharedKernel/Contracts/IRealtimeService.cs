namespace Turniejo.SharedKernel.Contracts;

public interface IRealtimeService
{
    Task PublishMatchScoreChangedAsync(Guid matchId, int homeScore, int awayScore, CancellationToken cancellationToken = default);
    Task PublishMatchStatusChangedAsync(Guid matchId, string status, CancellationToken cancellationToken = default);
    Task PublishMatchEventAddedAsync(Guid matchId, Guid matchEventId, string type, CancellationToken cancellationToken = default);
    Task PublishMatchEventRevertedAsync(Guid matchId, Guid matchEventId, CancellationToken cancellationToken = default);
    /// <summary>Wynik albo status meczu się zmienił — sygnał dla list meczów subskrybujących cały turniej.</summary>
    Task PublishTournamentMatchChangedAsync(Guid tournamentId, Guid matchId, CancellationToken cancellationToken = default);
    /// <summary>Nowe powiadomienie — wysyłane bezpośrednio do kont odbiorców (NotificationHub).</summary>
    Task PublishNotificationCreatedAsync(
        IReadOnlyCollection<Guid> userIds, Guid notificationId, string type, string title, string message,
        CancellationToken cancellationToken = default);
    Task PublishStandingUpdatedAsync(Guid tournamentId, Guid groupId, CancellationToken cancellationToken = default);
    Task PublishBracketUpdatedAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}
