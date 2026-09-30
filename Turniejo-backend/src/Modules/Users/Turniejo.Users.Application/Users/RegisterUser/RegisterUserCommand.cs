using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber = null,
    // Organizator dostaje od razu organizację (tworzy turnieje); rodzic/trener/sędzia nie — dołącza
    // kodem, a organizację może założyć później (POST /api/organizations). Domyślnie true, żeby stare
    // wywołania API zachowały dotychczasowe zachowanie.
    bool CreateOrganization = true,
    // Obowiązkowy checkbox "Akceptuję Regulamin i Politykę prywatności" — wersję dopisuje serwer.
    bool AcceptTerms = false) : ICommand<RegisterUserResponse>, IPublicRequest;