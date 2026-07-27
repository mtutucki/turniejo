using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber = null) : ICommand<RegisterUserResponse>, IPublicRequest;