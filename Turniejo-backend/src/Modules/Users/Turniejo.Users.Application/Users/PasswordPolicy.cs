using FluentValidation;

namespace Turniejo.Users.Application.Users;

/// <summary>
/// Polityka haseł wspólna dla rejestracji i resetu. Musi być 1:1 z passwordPolicy realmu
/// w Keycloaku (infra/keycloak/turniejo-realm.json) i z passwordSchema we frontendzie —
/// hasło odrzucone dopiero przez Keycloaka kończy się błędem 500 zamiast błędem pola.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxLength = 100;

    public static IRuleBuilderOptions<T, string> MeetsPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinLength).WithMessage($"Password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength)
            .Must(HasRequiredCharacters)
            .WithMessage("Password must contain a lowercase letter, an uppercase letter and a digit.");

    // Puste hasło zgłasza już NotEmpty — bez drugiego komunikatu dla tego samego pola.
    private static bool HasRequiredCharacters(string? password) =>
        string.IsNullOrEmpty(password)
        || (password.Any(char.IsLower) && password.Any(char.IsUpper) && password.Any(char.IsDigit));
}
