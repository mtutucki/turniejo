using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.SharedKernel.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserContext currentUserContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Pomiń autoryzację dla publicznych requestów
        if (request is CQRS.IPublicRequest)
            return await next();

        if (!currentUserContext.IsAuthenticated)
            throw new UnauthorizedException();

        return await next();
    }
}