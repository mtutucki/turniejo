namespace Turniejo.SharedKernel.Primitives;

public abstract class BaseEntity
{
    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    protected BaseEntity(Guid id)
    {
        Id = id;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    protected BaseEntity() { }

    public void SetUpdatedAt() => UpdatedAt = DateTimeOffset.UtcNow;
}