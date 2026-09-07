using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamPaymentStatus
{
    internal sealed class ChangeTournamentTeamPaymentStatusCommandHandler(
        ITeamsDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<ChangeTournamentTeamPaymentStatusCommand, ChangeTournamentTeamPaymentStatusResponse>
    {
        public async Task<Result<ChangeTournamentTeamPaymentStatusResponse>> Handle(ChangeTournamentTeamPaymentStatusCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<ChangeTournamentTeamPaymentStatusResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var result = tournamentTeam.ChangePaymentStatus(request.NewStatus, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<ChangeTournamentTeamPaymentStatusResponse>(result.Error);

            foreach (var domainEvent in tournamentTeam.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new ChangeTournamentTeamPaymentStatusResponse(tournamentTeam.Id));
        }
    }
}
