using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamRegistrationStatus
{
    internal sealed class ChangeTournamentTeamRegistrationStatusCommandHandler(
        ITeamsDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<ChangeTournamentTeamRegistrationStatusCommand, ChangeTournamentTeamRegistrationStatusResponse>
    {
        public async Task<Result<ChangeTournamentTeamRegistrationStatusResponse>> Handle(ChangeTournamentTeamRegistrationStatusCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<ChangeTournamentTeamRegistrationStatusResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var result = tournamentTeam.ChangeRegistrationStatus(request.NewStatus, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<ChangeTournamentTeamRegistrationStatusResponse>(result.Error);

            foreach (var domainEvent in tournamentTeam.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new ChangeTournamentTeamRegistrationStatusResponse(tournamentTeam.Id));
        }
    }
}
