using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.GetMyProfile;

// Dane osobowe zalogowanego usera do edycji na stronie "Moje konto" — z bazy, nie z tokenu,
// bo token ma tylko imię, nazwisko i e-mail (bez telefonu) i nie odświeża się po zmianie.
public sealed record GetMyProfileQuery : IQuery<MyProfileDto>, IAuthenticatedOnlyRequest;

public sealed record MyProfileDto(string Email, string FirstName, string LastName, string? PhoneNumber);
