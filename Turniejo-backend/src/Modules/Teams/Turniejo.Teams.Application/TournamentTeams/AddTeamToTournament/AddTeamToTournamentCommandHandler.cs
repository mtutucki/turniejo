using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;

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

            var alreadyRegistered = await dbContext.TournamentTeamExistsAsync(request.TournamentId, request.TeamId, request.CategoryId, cancellationToken);
            if (alreadyRegistered)
                return Result.Failure<AddTeamToTournamentResponse>(Error.Validation("TeamId", "Team is already registered for this tournament and category"));

            var acceptedCount = await dbContext.CountAcceptedTournamentTeamsAsync(request.TournamentId, cancellationToken);
            if (acceptedCount >= tournament.TeamLimit)
                return Result.Failure<AddTeamToTournamentResponse>(Error.Validation("TeamId", "Tournament has reached its team limit"));

            var result = TournamentTeam.Create(
                Guid.NewGuid(),
                request.TournamentId,
                request.TeamId,
                request.CategoryId,
                currentUserContext.UserId);

            if (result.IsFailure)
                return Result.Failure<AddTeamToTournamentResponse>(result.Error);

            var tournamentTeam = result.Value;

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
