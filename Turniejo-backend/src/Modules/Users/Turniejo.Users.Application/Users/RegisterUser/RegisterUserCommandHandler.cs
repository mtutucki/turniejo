using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.Application.Users.RegisterUser;

internal sealed class RegisterUserCommandHandler(
    IKeycloakAdminService keycloakAdminService,
    IUsersDbContext dbContext)
    : ICommandHandler<RegisterUserCommand, RegisterUserResponse>
{
    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        Guid keycloakUserId;
        try
        {
            keycloakUserId = await keycloakAdminService.CreateUserAsync(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("CONFLICT"))
        {
            return Result.Failure<RegisterUserResponse>(
                Error.Validation("Email", "User with this email already exists."));
        }


        var userResult = User.Create(
            keycloakUserId,
            request.Email,
            request.FirstName,
            request.LastName,
            request.PhoneNumber);

        if (userResult.IsFailure)
            return Result.Failure<RegisterUserResponse>(userResult.Error);

        var user = userResult.Value;

        await dbContext.AddUserAsync(user, cancellationToken);

        foreach (var domainEvent in user.DomainEvents)
        {
            var outboxMessage = OutboxMessage.Create(
                domainEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        user.ClearDomainEvents();

        return Result.Success(new RegisterUserResponse(user.Id));
    }
}