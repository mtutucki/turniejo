namespace Turniejo.SharedKernel.CQRS;

/// <summary>
/// Opcjonalny dodatkowy scope do sprawdzenia, gdy request implementuje IAuthorizedRequest,
/// ale dostęp może być przyznany też przez inną rolę w innym scope (np. Organizer turnieju
/// LUB Coach konkretnej TournamentTeam). AuthorizationBehavior przepuszcza request,
/// jeśli PRIMARY (IAuthorizedRequest) LUB ten alternatywny scope się zgadza.
/// </summary>
public interface IAlternateAuthorizedScope
{
    string AlternateScopeType { get; }
    Guid AlternateScopeId { get; }
    IReadOnlyCollection<string> AlternateAllowedRoles { get; }
}
