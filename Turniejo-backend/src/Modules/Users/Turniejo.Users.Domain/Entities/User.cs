using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Domain.Enums;
using Turniejo.Users.Domain.Events;

namespace Turniejo.Users.Domain.Entities;

public sealed class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public UserStatus Status { get; private set; }
    // RODO (rozliczalność): kiedy i którą wersję regulaminu/polityki prywatności user zaakceptował.
    public DateTimeOffset? TermsAcceptedAt { get; private set; }
    public string? TermsVersion { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private User(Guid id) : base(id) { }

    private User() { } // dla EF Core

    public static Result<User> Create(
        Guid id,
        string email,
        string firstName,
        string lastName,
        string? phoneNumber = null,
        bool createOrganization = true,
        string? acceptedTermsVersion = null)
    {
        if (id == Guid.Empty)
            return Result.Failure<User>(Error.Validation("Id", "User Id cannot be empty."));

        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<User>(Error.Validation("Email", "Email cannot be empty."));

        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure<User>(Error.Validation("FirstName", "First name cannot be empty."));

        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure<User>(Error.Validation("LastName", "Last name cannot be empty."));

        var user = new User(id);

        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.PhoneNumber = phoneNumber;
        user.Status = UserStatus.Active;

        if (acceptedTermsVersion is not null)
        {
            user.TermsAcceptedAt = DateTimeOffset.UtcNow;
            user.TermsVersion = acceptedTermsVersion;
        }

        user._domainEvents.Add(new UserRegistered(
            id, email, firstName, lastName, createOrganization));

        return Result.Success(user);
    }

    /// <summary>
    /// Usunięcie konta (prawo do usunięcia danych). Rekord zostaje, bo inne moduły trzymają jego Id
    /// (sędzia meczu, autor zdarzenia) — znikają tylko dane osobowe. Akceptacja regulaminu zostaje
    /// jako dowód zgodności.
    /// </summary>
    public void Anonymize()
    {
        Email = $"deleted-{Id:N}@deleted.invalid";
        FirstName = "Usunięty";
        LastName = "użytkownik";
        PhoneNumber = null;
        Status = UserStatus.Deleted;
        DeletedAt = DateTimeOffset.UtcNow;
        SetUpdatedAt();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}