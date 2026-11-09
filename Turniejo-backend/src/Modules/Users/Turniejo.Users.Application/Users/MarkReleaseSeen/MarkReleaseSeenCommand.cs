using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.MarkReleaseSeen;

// Zamknięcie okna "Co nowego": zapamiętuje wersję (X.Y.Z) na koncie z ICurrentUserContext.UserId,
// żeby okno nie wróciło na innym urządzeniu.
public sealed record MarkReleaseSeenCommand(string Version) : ICommand, IAuthenticatedOnlyRequest;
