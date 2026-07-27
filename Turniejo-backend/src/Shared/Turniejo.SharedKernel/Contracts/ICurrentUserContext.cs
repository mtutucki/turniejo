namespace Turniejo.SharedKernel.Contracts;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}