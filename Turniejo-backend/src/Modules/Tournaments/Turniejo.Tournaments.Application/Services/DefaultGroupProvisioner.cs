using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.Services
{
    /// <summary>
    /// Mały turniej ma jedną pulę na kategorię — organizator nie powinien musieć ręcznie tworzyć grupy,
    /// żeby wygenerować terminarz. W formatach z fazą grupową każda kategoria bez grup dostaje
    /// "Grupa A", a drużyny tej kategorii bez grupy trafiają do niej. Kto chce więcej grup, dodaje je
    /// i przesuwa drużyny. Wołane po dodaniu kategorii i po ustawieniu formatu.
    /// </summary>
    public sealed class DefaultGroupProvisioner(
        ITournamentsDbContext dbContext,
        ITeamModule teamModule,
        ICurrentUserContext currentUserContext)
    {
        public const string DefaultGroupName = "Grupa A";

        public async Task EnsureDefaultGroupsAsync(
            Tournament tournament,
            IReadOnlyCollection<Guid> categoryIds,
            CancellationToken cancellationToken = default)
        {
            if (tournament.Format is not (TournamentFormat.Group or TournamentFormat.GroupAndKnockout))
                return;

            // Po wygenerowaniu terminarza struktura grup jest zamrożona.
            if (tournament.Status >= TournamentStatus.ScheduleGenerated)
                return;

            foreach (var categoryId in categoryIds)
            {
                var existing = await dbContext.GetTournamentGroupsByCategoryIdAsync(categoryId, cancellationToken);
                if (existing.Count > 0)
                    continue;

                var groupResult = TournamentGroup.Create(
                    Guid.NewGuid(), tournament.OrganizationId, tournament.Id, currentUserContext.UserId,
                    categoryId, DefaultGroupName, 1);
                if (groupResult.IsFailure)
                    continue;

                var group = groupResult.Value;
                await dbContext.AddTournamentGroupAsync(group, cancellationToken);
                foreach (var domainEvent in group.DomainEvents)
                {
                    await dbContext.AddOutboxMessageAsync(OutboxMessage.Create(
                        domainEvent.GetType().AssemblyQualifiedName!,
                        JsonSerializer.Serialize(domainEvent, domainEvent.GetType())), cancellationToken);
                }
                await dbContext.SaveChangesAsync(cancellationToken);

                // Drużyny należą do modułu Teams — przypisanie przez jego kontrakt.
                await teamModule.AssignUngroupedTeamsToGroupAsync(tournament.Id, categoryId, group.Id, cancellationToken);
            }
        }
    }
}
