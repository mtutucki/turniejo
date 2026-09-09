using Microsoft.EntityFrameworkCore;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options), INotificationsDbContext
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken = default) =>
        await Notifications.AddAsync(notification, cancellationToken);

    public async Task<Notification?> GetNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
        await Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

    // Filtr po Type jest tu krytyczny: bez niego JAKAKOLWIEK notyfikacja o tym meczu (np.
    // MatchTimeChanged po zwykłej zmianie godziny) sprawiała, że to zapytanie myślało, że
    // przypomnienie już wysłano, i mecz nigdy go nie dostawał.
    public async Task<bool> MatchReminderExistsAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        await Notifications.AnyAsync(
            n => n.MatchId == matchId && n.Type == NotificationType.MatchReminder,
            cancellationToken);

    public async Task AddNotificationRecipientAsync(NotificationRecipient recipient, CancellationToken cancellationToken = default) =>
        await NotificationRecipients.AddAsync(recipient, cancellationToken);

    public async Task AddNotificationRecipientsAsync(IReadOnlyCollection<NotificationRecipient> recipients, CancellationToken cancellationToken = default) =>
        await NotificationRecipients.AddRangeAsync(recipients, cancellationToken);

    public async Task<NotificationRecipient?> GetNotificationRecipientByIdAsync(Guid notificationRecipientId, CancellationToken cancellationToken = default) =>
        await NotificationRecipients.FirstOrDefaultAsync(r => r.Id == notificationRecipientId, cancellationToken);

    public async Task<IReadOnlyList<NotificationRecipient>> GetNotificationRecipientsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await NotificationRecipients
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        await OutboxMessages.AddAsync(message, cancellationToken);

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await base.SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.FindPrimaryKey()?.Properties
                .Any(p => p.ClrType == typeof(Guid)) == true)
            {
                entity.FindPrimaryKey()!.Properties
                    .Where(p => p.ClrType == typeof(Guid))
                    .ToList()
                    .ForEach(p => p.ValueGenerated =
                        Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never);
            }
        }

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
