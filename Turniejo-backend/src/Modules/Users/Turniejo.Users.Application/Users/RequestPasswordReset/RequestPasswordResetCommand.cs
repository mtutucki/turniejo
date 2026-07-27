using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email)
    : ICommand, IPublicRequest;