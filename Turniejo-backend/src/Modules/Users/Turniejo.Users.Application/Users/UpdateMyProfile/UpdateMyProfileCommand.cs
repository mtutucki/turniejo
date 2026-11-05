using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.UpdateMyProfile;

// Tylko konto z ICurrentUserContext.UserId. E-mail się nie zmienia — to login w Keycloaku.
public sealed record UpdateMyProfileCommand(string FirstName, string LastName, string? PhoneNumber)
    : ICommand, IAuthenticatedOnlyRequest;
