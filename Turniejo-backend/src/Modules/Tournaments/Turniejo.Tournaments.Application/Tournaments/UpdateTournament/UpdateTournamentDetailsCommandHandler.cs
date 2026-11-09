using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournament
{
    internal sealed class UpdateTournamentDetailsCommandHandler(ITournamentsDbContext dbContext, ITeamModule teamModule, IMatchModule matchModule) : ICommandHandler<UpdateTournamentDetailsCommand, UpdateTournamentDetailsResponse>
    {
        public async Task<Result<UpdateTournamentDetailsResponse>> Handle(UpdateTournamentDetailsCommand request, CancellationToken cancellationToken)
        {
            var result = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (result == null)
                return Result.Failure<UpdateTournamentDetailsResponse>(Error.NotFound("Tournament", request.TournamentId));

            // Optymistyczna kontrola zapisu (QA v3: UST-015): drugi organizator bez odświeżenia nadpisywał
            // zmiany pierwszego. Tolerancja 1 ms na zaokrąglenie czasu w bazie (mikrosekundy).
            if (request.ExpectedUpdatedAt is { } expected &&
                (result.UpdatedAt - expected).Duration() > TimeSpan.FromMilliseconds(1))
                return Result.Failure<UpdateTournamentDetailsResponse>(Error.Conflict(
                    "UpdatedAt", "The tournament was changed by someone else. Refresh to see the latest data."));

            // Limit nie może spaść poniżej liczby już zaakceptowanych drużyn, inaczej turniej ma ich więcej,
            // niż pozwala. Sprawdzamy tylko przy obniżaniu, żeby zwykła edycja nie pytała modułu Teams.
            if (request.TeamLimit < result.TeamLimit)
            {
                var tournamentTeams = await teamModule.GetTournamentTeamsByTournamentIdAsync(request.TournamentId, cancellationToken);
                var acceptedCount = tournamentTeams.Count(t => t.RegistrationStatus == "Accepted");
                if (request.TeamLimit < acceptedCount)
                    return Result.Failure<UpdateTournamentDetailsResponse>(Error.Validation("TeamLimit", "Team limit cannot be lower than the number of accepted teams"));
            }

            // Mecz poza nowymi datami zostałby w terminarzu po zakończeniu (albo przed startem) turnieju.
            // Decyzja: najpierw trzeba go przesunąć. Sprawdzamy tylko, gdy daty się zmieniają.
            if (request.StartAt != result.StartAt || request.EndAt != result.EndAt)
            {
                var duration = TimeSpan.FromMinutes(result.Settings?.MatchDurationMinutes ?? 0);
                var matches = await matchModule.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken);
                if (matches.Any(m => m.Status != "Cancelled" &&
                                     (m.ScheduledAt < request.StartAt || m.ScheduledAt + duration > request.EndAt)))
                    return Result.Failure<UpdateTournamentDetailsResponse>(
                        Error.Validation("EndAt", "Some matches fall outside the new tournament dates"));
            }

            var updateResult = result.UpdateDetails(request.Name, request.StartAt, request.EndAt, request.TeamLimit, request.Description);

            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentDetailsResponse>(updateResult.Error);

            foreach (var domainEvent in result.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            result.ClearDomainEvents();
            return Result.Success(new UpdateTournamentDetailsResponse(result.Id, result.Slug));
        }
    }
}
