using Turniejo.Tournaments.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.JoinTournament
{
    internal sealed class JoinTournamentCommandHandler(ITournamentsDbContext dbContext, ICurrentUserContext currentUserContext, IUserModule userModule) : ICommandHandler<JoinTournamentCommand, JoinTournamentResponse>
    {
        public async Task<Result<JoinTournamentResponse>> Handle(JoinTournamentCommand request, CancellationToken cancellationToken)
        {
            var result = await dbContext.GetTournamentByCodeAsync(request.Code, cancellationToken);

            if (result == null)
                return Result.Failure<JoinTournamentResponse>(Error.Validation("Code", "Invalid tournament code."));

            // Decyzja QA v3 (KON-018): anulowany turniej nie przyjmuje nowych osób; zakończony - tak, do wglądu.
            if (result.Status == TournamentStatus.Cancelled)
                return Result.Failure<JoinTournamentResponse>(Error.Validation("Code", "This tournament has been cancelled"));

            var role = result.RefereeCode == request.Code ? nameof(UserRole.Referee) :
               result.CoachCode == request.Code ? nameof(UserRole.Coach) :
               nameof(UserRole.Viewer);

            await userModule.AssignRoleAsync(currentUserContext.UserId, role, nameof(RoleScopeType.Tournament), result.Id, cancellationToken);

            return Result.Success(new JoinTournamentResponse(result.Id, result.Name, result.Slug, role, result.PublicSettings.IsPublic));
        }
    }
}
