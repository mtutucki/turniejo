using MediatR;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.Users.Domain.Events;

namespace Turniejo.Organizations.Application.EventHandlers
{
    // Domyka przepływ z arkusza: "Handler UserRegistered (Outbox) -> CreateOrganizationCommand".
    // Idempotentny, bo Outbox gwarantuje tylko at-least-once delivery (retry po wyjątku) — druga
    // dostawa tego samego eventu (albo równoległy POST /api/organizations) musi zastać spójny stan,
    // a nie zdublować organizację czy zawieszony wyjątek.
    internal sealed class UserRegisteredHandler(
        IOrganizationsDbContext dbContext,
        OrganizationProvisioner provisioner,
        IUserModule userModule) : INotificationHandler<UserRegistered>
    {
        public async Task Handle(UserRegistered notification, CancellationToken cancellationToken)
        {
            var organization = await dbContext.GetOrganizationByOwnerUserIdAsync(notification.UserId, cancellationToken);

            if (organization is null)
            {
                var name = $"{notification.FirstName} {notification.LastName}".Trim();
                var result = await provisioner.CreateAsync(
                    notification.UserId,
                    string.IsNullOrWhiteSpace(name) ? notification.Email : name,
                    cancellationToken);

                if (result.IsSuccess)
                {
                    organization = result.Value;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    // "Już istnieje" (retry Outboxa po wcześniejszym częściowym sukcesie, albo wyścig
                    // z ręcznym POST /api/organizations, złapany przez unikalny indeks) nie jest błędem —
                    // po prostu organizacja już jest, więc tylko domykamy przypisanie roli poniżej.
                    // Każdy inny błąd (np. pusta nazwa) oznacza, że coś jest naprawdę nie tak — retry
                    // Outboxa i tak nic by tu nie naprawił, ale ma zostać w logach jako failed message.
                    organization = await dbContext.GetOrganizationByOwnerUserIdAsync(notification.UserId, cancellationToken);
                    if (organization is null)
                        throw new DomainException($"Failed to create organization for user {notification.UserId}: {result.Error.Message}");
                }
            }

            // AssignRoleAsync jest idempotentny (sprawdza istniejące przypisanie przed insertem) —
            // bezpieczne wołanie także wtedy, gdy CreateOrganizationCommand zdążył to już zrobić.
            await userModule.AssignRoleAsync(
                notification.UserId,
                nameof(UserRole.Organizer),
                nameof(RoleScopeType.Organization),
                organization.Id,
                cancellationToken);
        }
    }
}
