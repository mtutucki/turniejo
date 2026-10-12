using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Users.Domain.Entities;

/// <summary>
/// Jednorazowy link do ustawienia nowego hasła. W bazie leży tylko skrót tokenu — sam token zna
/// wyłącznie odbiorca maila, więc wyciek tabeli nie pozwala przejąć konta.
/// </summary>
public sealed class PasswordResetToken : BaseEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    private PasswordResetToken(Guid id) : base(id) { }
    private PasswordResetToken() { }

    public static PasswordResetToken Create(Guid userId, string tokenHash) =>
        new(Guid.NewGuid())
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime)
        };

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    public void MarkUsed()
    {
        UsedAt = DateTimeOffset.UtcNow;
        SetUpdatedAt();
    }
}
