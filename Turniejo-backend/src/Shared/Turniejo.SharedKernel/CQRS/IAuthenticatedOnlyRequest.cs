namespace Turniejo.SharedKernel.CQRS;

/// <summary>
/// Oznacza command/query, dla którego bycie zalogowanym to świadomie cała autoryzacja — albo
/// handler sam filtruje/sprawdza dostęp względem ICurrentUserContext.UserId (np. "moje dzieci",
/// "mój zespół", "mój komentarz"), albo akcja jest bezpieczna dla każdego zalogowanego użytkownika
/// (np. utworzenie własnego zasobu). Nie zmienia zachowania AuthorizationBehavior — bez tego markera
/// (i bez IAuthorizedRequest/IPublicRequest) pipeline już dziś wymaga tylko IsAuthenticated. Ten
/// interfejs istnieje wyłącznie po to, żeby test architektoniczny (AuthorizationCoverageTests) mógł
/// odróżnić świadomą decyzję od zapomnianego markera.
/// </summary>
public interface IAuthenticatedOnlyRequest
{
}
