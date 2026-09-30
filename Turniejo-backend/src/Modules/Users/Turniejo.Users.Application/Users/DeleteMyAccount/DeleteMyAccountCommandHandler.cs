using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Users.DeleteMyAccount;

/// <summary>
/// Usunięcie konta na żądanie usera (RODO art. 17): anonimizacja profilu, usunięcie ról i powiązań
/// z dziećmi, na końcu konto w Keycloaku. Idempotentny — jeśli Keycloak zawiedzie po zapisie w bazie,
/// user (wciąż z ważnym tokenem) ponawia i handler domyka już tylko Keycloaka.
/// </summary>
internal sealed class DeleteMyAccountCommandHandler(
    IUsersDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IKeycloakAdminService keycloakAdminService,
    IPlayerModule playerModule) : ICommandHandler<DeleteMyAccountCommand>
{
    public async Task<Result> Handle(DeleteMyAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.UserId;
        var user = await dbContext.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("User", userId));

        if (user.Status != UserStatus.Deleted)
        {
            var roles = await dbContext.GetUserRolesAsync(userId, cancellationToken);

            // Organizacja jedynego organizatora (turnieje, baza drużyn, zawodnicy — dane innych osób)
            // zostałaby bez administratora. Jej usunięcie idzie przez kontakt, nie automatycznie.
            foreach (var organization in roles.Where(r => r.Role == UserRole.Organizer && r.ScopeType == RoleScopeType.Organization && r.ScopeId.HasValue))
            {
                var members = await dbContext.GetUserRoleAssignmentsByScopeAsync(RoleScopeType.Organization, organization.ScopeId!.Value, cancellationToken);
                if (!members.Any(m => m.UserId != userId && m.Role == UserRole.Organizer))
                    return Result.Failure(Error.Conflict("Account", "You are the only organizer of an organization. Contact support to delete it together with your account."));
            }

            await playerModule.RemoveGuardianshipsAsync(userId, cancellationToken);
            dbContext.RemoveUserRoleAssignments(roles);
            user.Anonymize();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await keycloakAdminService.DeleteUserAsync(userId, cancellationToken);
        return Result.Success();
    }
}
