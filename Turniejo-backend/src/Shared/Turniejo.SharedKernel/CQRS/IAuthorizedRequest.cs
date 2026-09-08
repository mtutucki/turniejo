namespace Turniejo.SharedKernel.CQRS;

/// <summary>
/// Oznacza command/query operujący na konkretnym zasobie — AuthorizationBehavior
/// odrzuci request, jeśli aktualny użytkownik nie ma żadnej z AllowedRoles w danym scope.
/// </summary>
public interface IAuthorizedRequest
{
    string ScopeType { get; }
    Guid ScopeId { get; }
    IReadOnlyCollection<string> AllowedRoles { get; }
}
