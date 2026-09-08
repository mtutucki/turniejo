using System.Text.Json;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.AssignCoachToTeam
{
    internal sealed class AssignCoachToTeamCommandHandler(
        ITeamsDbContext dbContext,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignCoachToTeamCommand, AssignCoachToTeamResponse>
    {
        public async Task<Result<AssignCoachToTeamResponse>> Handle(AssignCoachToTeamCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<AssignCoachToTeamResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var alreadyAssigned = await dbContext.TeamStaffExistsAsync(request.TournamentTeamId, request.UserId, TeamStaffRole.Coach, cancellationToken);
            if (alreadyAssigned)
                return Result.Failure<AssignCoachToTeamResponse>(Error.Validation("UserId", "User is already a coach for this team"));

            var result = TeamStaff.CreateCoach(
                Guid.NewGuid(),
                request.TournamentTeamId,
                request.UserId,
                currentUserContext.UserId);

            if (result.IsFailure)
                return Result.Failure<AssignCoachToTeamResponse>(result.Error);

            var teamStaff = result.Value;

            await dbContext.AddTeamStaffAsync(teamStaff, cancellationToken);

            await userModule.AssignRoleAsync(
                request.UserId,
                nameof(UserRole.Coach),
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

            return Result.Success(new AssignCoachToTeamResponse(teamStaff.Id));
        }
    }
}
