using Microsoft.EntityFrameworkCore;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Notifications.Infrastructure.Persistence;

internal sealed class OutboxRepository(NotificationsDbContext dbContext) : IOutboxRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize = 20,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await dbContext.OutboxMessages
            .Where(x => x.ProcessedAt == null && x.RetryCount < OutboxMessage.MaxRetryCount &&
                        (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}