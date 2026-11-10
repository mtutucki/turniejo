using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamRegistrationStatus
{
    internal sealed class ChangeTournamentTeamRegistrationStatusCommandHandler(
        ITeamsDbContext dbContext,
        ITournamentModule tournamentModule,
        IMatchModule matchModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<ChangeTournamentTeamRegistrationStatusCommand, ChangeTournamentTeamRegistrationStatusResponse>
    {
        public async Task<Result<ChangeTournamentTeamRegistrationStatusResponse>> Handle(ChangeTournamentTeamRegistrationStatusCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<ChangeTournamentTeamRegistrationStatusResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            // Wycofanie w trakcie meczu odwołałoby mecz, który właśnie trwa. Decyzja: najpierw koniec meczu.
            if (request.NewStatus == RegistrationStatus.Withdrawn)
            {
                var matches = await matchModule.GetMatchesByTournamentTeamIdsAsync([tournamentTeam.Id], cancellationToken);
                if (matches.Any(m => m.Status is "Live" or "Paused"))
                    return Result.Failure<ChangeTournamentTeamRegistrationStatusResponse>(
                        Error.Conflict("RegistrationStatus", "The team is playing a match right now"));
            }

            // Limit drużyn liczy zaakceptowane zgłoszenia. Sprawdzenie tylko przy zgłaszaniu nie wystarczało:
            // oczekujących mogło być więcej niż miejsc, a akceptacja przepuszczała wszystkie.
            if (request.NewStatus == RegistrationStatus.Accepted && tournamentTeam.RegistrationStatus != RegistrationStatus.Accepted)
            {
                var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
                if (tournament == null)
                    return Result.Failure<ChangeTournamentTeamRegistrationStatusResponse>(Error.NotFound("Tournament", request.TournamentId));
                // Drużyna przyjęta po generowaniu nie miałaby żadnego meczu.
                if (ScheduleLock.IsLocked(tournament))
                    return Result.Failure<ChangeTournamentTeamRegistrationStatusResponse>(ScheduleLock.Error);

                var acceptedCount = await dbContext.CountAcceptedTournamentTeamsAsync(request.TournamentId, cancellationToken);
                if (acceptedCount >= tournament.TeamLimit)
                    return Result.Failure<ChangeTournamentTeamRegistrationStatusResponse>(Error.Validation("RegistrationStatus", "Tournament has reached its team limit"));
            }

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
