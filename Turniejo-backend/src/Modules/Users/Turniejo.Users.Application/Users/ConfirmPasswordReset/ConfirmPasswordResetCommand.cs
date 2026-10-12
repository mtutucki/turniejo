using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.ConfirmPasswordReset;

public sealed record ConfirmPasswordResetCommand(string Token, string NewPassword)
    : ICommand, IPublicRequest;
