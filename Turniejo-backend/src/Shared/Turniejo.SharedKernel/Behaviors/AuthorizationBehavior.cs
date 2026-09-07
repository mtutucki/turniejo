using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.SharedKernel.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserContext currentUserContext,
    IAuthorizationService authorizationService)
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
        if (request is IPublicRequest)
            return await next();

        if (!currentUserContext.IsAuthenticated)
            throw new UnauthorizedException();

        // Dla requestów operujących na konkretnym zasobie — sprawdź czy user ma
        // jedną z wymaganych ról w danym scope (np. Organizer w Tournament).
        if (request is IAuthorizedRequest authorizedRequest)
        {
            var hasAccess = await authorizationService.HasAnyRoleAsync(
                currentUserContext.UserId,
                authorizedRequest.ScopeType,
                authorizedRequest.ScopeId,
                authorizedRequest.AllowedRoles,
                cancellationToken);

            // Jeśli primary scope nie przepuszcza, sprawdź alternatywny scope
            // (np. Coach konkretnej TournamentTeam zamiast Organizer całego turnieju).
            if (!hasAccess && request is IAlternateAuthorizedScope alternateScope)
            {
                hasAccess = await authorizationService.HasAnyRoleAsync(
                    currentUserContext.UserId,
                    alternateScope.AlternateScopeType,
                    alternateScope.AlternateScopeId,
                    alternateScope.AlternateAllowedRoles,
                    cancellationToken);
            }

            if (!hasAccess)
                throw new UnauthorizedException();
        }

        return await next();
    }
}