using Microsoft.EntityFrameworkCore;
using Turniejo.Notifications.Infrastructure.Persistence;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Users.Infrastructure.Persistence;

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
        return await dbContext.OutboxMessages
            .Where(x => x.ProcessedAt == null && x.RetryCount < 5)
            .OrderBy(x => x.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}