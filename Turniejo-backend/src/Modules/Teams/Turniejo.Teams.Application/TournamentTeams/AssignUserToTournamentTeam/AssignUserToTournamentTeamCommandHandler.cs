using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Application.TournamentTeams.AssignUserToTournamentTeam
{
    internal sealed class AssignUserToTournamentTeamCommandHandler(
        ITeamsDbContext dbContext,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignUserToTournamentTeamCommand, AssignUserToTournamentTeamResponse>
    {
        public async Task<Result<AssignUserToTournamentTeamResponse>> Handle(AssignUserToTournamentTeamCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<AssignUserToTournamentTeamResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var alreadyAssigned = await dbContext.TeamStaffExistsAsync(request.TournamentTeamId, request.UserId, request.Role, cancellationToken);
            if (alreadyAssigned)
                return Result.Failure<AssignUserToTournamentTeamResponse>(Error.Validation("UserId", "User already has this role for this team"));

            var result = TeamStaff.Create(
                Guid.NewGuid(),
                request.TournamentTeamId,
                request.UserId,
                request.Role,
                currentUserContext.UserId);

            if (result.IsFailure)
                return Result.Failure<AssignUserToTournamentTeamResponse>(result.Error);

            var teamStaff = result.Value;

            await dbContext.AddTeamStaffAsync(teamStaff, cancellationToken);

            await userModule.AssignRoleAsync(
                request.UserId,
                request.Role.ToString(),
                nameof(RoleScopeType.TournamentTeam),
                request.TournamentTeamId,
                cancellationToken);

            foreach (var domainEvent in teamStaff.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignUserToTournamentTeamResponse(teamStaff.Id));
        }
    }
}
