using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.ChangeMyPassword;

// Wymaga obecnego hasła: sama zalogowana sesja (np. zostawiona na cudzym komputerze) nie wystarczy.
public sealed record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword)
    : ICommand, IAuthenticatedOnlyRequest;
