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
    // Ostatnia wersja z okna "Co nowego", którą user zamknął (X.Y.Z). Null = nowe konto, okna jeszcze
    // nie było: frontend zapisuje wtedy bieżącą wersję bez pokazywania (docs/decyzje.md).
    public string? SeenReleaseVersion { get; private set; }

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
        string? acceptedTermsVersion = null,
        string? organizationName = null)
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
            id, email, firstName, lastName, createOrganization,
            string.IsNullOrWhiteSpace(organizationName) ? null : organizationName.Trim()));

        return Result.Success(user);
    }

    /// <summary>
    /// Zmiana danych osobowych przez samego usera (prawo do sprostowania danych). E-mail zostaje,
    /// bo jest loginem w Keycloaku — jego zmiana wymagałaby potwierdzenia nowego adresu.
    /// </summary>
    public Result UpdateProfile(string firstName, string lastName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure(Error.Validation("FirstName", "First name cannot be empty."));

        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure(Error.Validation("LastName", "Last name cannot be empty."));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        SetUpdatedAt();
        return Result.Success();
    }

    /// <summary>
    /// Zapamiętuje wersję z okna "Co nowego". Tylko w przód: karta otwarta przed wdrożeniem nie cofnie
    /// zapisu i nie pokaże drugi raz tych samych zmian.
    /// </summary>
    public Result MarkReleaseSeen(string version)
    {
        if (!TryParseRelease(version, out var parsed))
            return Result.Failure(Error.Validation("Version", "Release version must look like 1.2.0."));

        if (SeenReleaseVersion is not null && TryParseRelease(SeenReleaseVersion, out var current) && parsed <= current)
            return Result.Success();

        SeenReleaseVersion = version;
        SetUpdatedAt();
        return Result.Success();
    }

    private static bool TryParseRelease(string? value, out Version version)
    {
        version = new Version();
        var parts = value?.Split('.');
        return parts is { Length: 3 }
            && parts.All(p => p.Length is > 0 and <= 5 && p.All(char.IsAsciiDigit))
            && Version.TryParse(value, out version!);
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