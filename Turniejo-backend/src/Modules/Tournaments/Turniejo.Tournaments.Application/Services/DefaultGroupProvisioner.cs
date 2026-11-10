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
        public const int MaxGroupsOnCreate = 8;

        /// <summary>"Grupa A" dla 0, "Grupa B" dla 1…</summary>
        public static string GroupName(int index) => $"Grupa {(char)('A' + index)}";

        public Task EnsureDefaultGroupsAsync(
            Tournament tournament,
            IReadOnlyCollection<Guid> categoryIds,
            CancellationToken cancellationToken = default) =>
            EnsureDefaultGroupsAsync(tournament, categoryIds, 1, cancellationToken);

        /// <param name="groupCount">Ile grup dostaje kategoria bez grup (organizator wybiera przy dodawaniu kategorii).</param>
        public async Task EnsureDefaultGroupsAsync(
            Tournament tournament,
            IReadOnlyCollection<Guid> categoryIds,
            int groupCount,
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

                Guid? firstGroupId = null;
                for (var i = 0; i < Math.Clamp(groupCount, 1, MaxGroupsOnCreate); i++)
                {
                    var groupResult = TournamentGroup.Create(
                        Guid.NewGuid(), tournament.OrganizationId, tournament.Id, currentUserContext.UserId,
                        categoryId, GroupName(i), i + 1);
                    if (groupResult.IsFailure)
                        continue;

                    var group = groupResult.Value;
                    firstGroupId ??= group.Id;
                    await dbContext.AddTournamentGroupAsync(group, cancellationToken);
                    foreach (var domainEvent in group.DomainEvents)
                    {
                        await dbContext.AddOutboxMessageAsync(OutboxMessage.Create(
                            domainEvent.GetType().AssemblyQualifiedName!,
                            JsonSerializer.Serialize(domainEvent, domainEvent.GetType())), cancellationToken);
                    }
                }
                if (firstGroupId is not { } groupId)
                    continue;
                await dbContext.SaveChangesAsync(cancellationToken);

                // Drużyny należą do modułu Teams — przypisanie przez jego kontrakt. Bez grupy były dotąd
                // wszystkie, więc trafiają do pierwszej; organizator rozdziela je potem w Drużynach.
                await teamModule.AssignUngroupedTeamsToGroupAsync(tournament.Id, categoryId, groupId, cancellationToken);
            }
        }
    }
}
