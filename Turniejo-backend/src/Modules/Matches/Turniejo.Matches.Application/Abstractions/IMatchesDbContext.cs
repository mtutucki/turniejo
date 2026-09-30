using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.Application.Abstractions
{
    public interface IMatchesDbContext
    {
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Pitch
        Task AddPitchAsync(Pitch pitch, CancellationToken cancellationToken = default);
        Task<Pitch?> GetPitchByIdAsync(Guid pitchId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Pitch>> GetPitchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Pitch>> GetPitchesByIdsAsync(IReadOnlyCollection<Guid> pitchIds, CancellationToken cancellationToken = default);
        Task<bool> PitchNameExistsAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default);
        Task RemovePitchAsync(Pitch pitch, CancellationToken cancellationToken = default);

        // Match
        Task AddMatchAsync(Match match, CancellationToken cancellationToken = default);
        Task AddMatchesAsync(IReadOnlyCollection<Match> matches, CancellationToken cancellationToken = default);
        Task RemoveMatchesAsync(IReadOnlyCollection<Match> matches, CancellationToken cancellationToken = default);
        Task<Match?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Match>> GetMatchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Match>> GetMatchesByIdsAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Match>> GetFinishedMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
        Task<bool> MatchExistsAtPitchAndTimeAsync(Guid pitchId, DateTimeOffset scheduledAt, Guid? excludeMatchId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Match>> GetMatchesByTournamentTeamIdsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Match>> GetMatchesScheduledBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
        Task<bool> AnyMatchesByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
        Task<bool> AnyMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
        Task<bool> AnyMatchesByPitchIdAsync(Guid pitchId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Match>> GetCancellableMatchesByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Match>> GetCancellableMatchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);

        // MatchEvent
        Task AddMatchEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MatchEvent>> GetMatchEventsByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MatchEvent>> GetActiveMatchEventsByTypeAsync(Guid matchId, MatchEventType type, CancellationToken cancellationToken = default);
        Task<MatchEvent?> GetMatchEventByIdAsync(Guid matchEventId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MatchEvent>> GetMatchEventsByTournamentIdAndTypesAsync(Guid tournamentId, IReadOnlyCollection<MatchEventType> types, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MatchEvent>> GetMatchEventsByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);

        // AuditLog
        Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    }
}
