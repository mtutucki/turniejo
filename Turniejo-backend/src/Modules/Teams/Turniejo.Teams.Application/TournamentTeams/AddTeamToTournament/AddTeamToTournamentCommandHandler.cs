using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.AddTeamToTournament
{
    internal sealed class AddTeamToTournamentCommandHandler(
        ITeamsDbContext dbContext,
        ITournamentModule tournamentModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<AddTeamToTournamentCommand, AddTeamToTournamentResponse>
    {
        public async Task<Result<AddTeamToTournamentResponse>> Handle(AddTeamToTournamentCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<AddTeamToTournamentResponse>(Error.NotFound("Tournament", request.TournamentId));

            var team = await dbContext.GetTeamByIdAsync(request.TeamId, cancellationToken);
            if (team == null)
                return Result.Failure<AddTeamToTournamentResponse>(Error.NotFound("Team", request.TeamId));

            if (team.OrganizationId != tournament.OrganizationId)
                return Result.Failure<AddTeamToTournamentResponse>(Error.Validation("TeamId", "Team does not belong to the tournament's organization"));

            // Odrzucona/wycofana drużyna może zostać zgłoszona ponownie — wznawiamy jej wpis (unikalny indeks
            // turniej+drużyna+kategoria nie pozwala na drugi). Aktywne zgłoszenie to nadal błąd.
            var existing = await dbContext.GetTournamentTeamForUpdateAsync(request.TournamentId, request.TeamId, request.CategoryId, cancellationToken);
            if (existing != null && existing.RegistrationStatus is not (RegistrationStatus.Rejected or RegistrationStatus.Withdrawn))
                return Result.Failure<AddTeamToTournamentResponse>(Error.Validation("TeamId", "Team is already registered for this tournament and category"));

            var acceptedCount = await dbContext.CountAcceptedTournamentTeamsAsync(request.TournamentId, cancellationToken);
            if (acceptedCount >= tournament.TeamLimit)
                return Result.Failure<AddTeamToTournamentResponse>(Error.Validation("TeamId", "Tournament has reached its team limit"));

            TournamentTeam tournamentTeam;
            if (existing != null)
            {
                var reapplied = existing.Reapply(currentUserContext.UserId);
                if (reapplied.IsFailure)
                    return Result.Failure<AddTeamToTournamentResponse>(reapplied.Error);
                tournamentTeam = existing;
            }
            else
            {
                var result = TournamentTeam.Create(
                    Guid.NewGuid(),
                    request.TournamentId,
                    request.TeamId,
                    request.CategoryId,
                    currentUserContext.UserId);

                if (result.IsFailure)
                    return Result.Failure<AddTeamToTournamentResponse>(result.Error);

                tournamentTeam = result.Value;
            }

            // Kategoria z jedną grupą (np. domyślna "Grupa A") — drużyna od razu w niej, bez ręcznego
            // przypisywania. Przy kilku grupach wybór zostaje organizatorowi.
            if (tournamentTeam.GroupId == null)
            {
                var categoryGroups = (await tournamentModule.GetTournamentGroupsAsync(request.TournamentId, cancellationToken))
                    .Where(g => g.CategoryId == request.CategoryId)
                    .ToList();
                if (categoryGroups.Count == 1)
                    tournamentTeam.AssignToGroup(categoryGroups[0].Id);
            }

            if (existing == null)
                await dbContext.AddTournamentTeamAsync(tournamentTeam, cancellationToken);

            foreach (var domainEvent in tournamentTeam.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddTeamToTournamentResponse(tournamentTeam.Id));
        }
    }
}
