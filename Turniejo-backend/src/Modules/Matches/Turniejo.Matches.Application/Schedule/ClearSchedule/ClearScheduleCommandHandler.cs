using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Schedule.ClearSchedule
{
    internal sealed class ClearScheduleCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        IUserModule userModule) : ICommandHandler<ClearScheduleCommand, ClearScheduleResponse>
    {
        private static readonly MatchStatus[] StartedStatuses = [MatchStatus.Live, MatchStatus.Paused, MatchStatus.Finished];

        public async Task<Result<ClearScheduleResponse>> Handle(ClearScheduleCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<ClearScheduleResponse>(Error.NotFound("Tournament", request.TournamentId));

            // RegistrationClosed też wpuszczamy: to stan po nieudanym generowaniu (mecze zapisane, etap nie
            // zmieniony), z którego inaczej nie byłoby wyjścia — generator odmawia, gdy mecze już istnieją.
            if (tournament.Status != "ScheduleGenerated" && tournament.Status != "RegistrationClosed")
                return Result.Failure<ClearScheduleResponse>(Error.Validation("Status", "Schedule can only be cleared before the tournament starts"));

            var matches = await dbContext.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken);
            if (matches.Any(m => StartedStatuses.Contains(m.Status)))
                return Result.Failure<ClearScheduleResponse>(Error.Conflict("Schedule", "Cannot clear the schedule after a match has started"));

            // Puchar: mecze 1. rundy należą do drabinki (węzły wskazują na nie po MatchId) — usunięcie
            // zostawiłoby drabinkę bez meczów. Cofamy tylko etap; ponowne generowanie przestawi terminy.
            if (tournament.Format == "Knockout")
            {
                if (tournament.Status == "ScheduleGenerated" &&
                    !await tournamentModule.RevertScheduleGenerationAsync(request.TournamentId, cancellationToken))
                    return Result.Failure<ClearScheduleResponse>(Error.Conflict("Status", "The tournament stage could not be reverted — try again"));

                return Result.Success(new ClearScheduleResponse(0));
            }

            // Sędzia zachowałby rolę Referee w usuniętym meczu (UserRoleAssignment ze scope Match).
            foreach (var match in matches.Where(m => m.RefereeId.HasValue))
            {
                await userModule.RevokeRoleAsync(
                    match.RefereeId!.Value, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, cancellationToken);
            }

            // Najpierw mecze, potem etap: gdy cofnięcie etapu się nie uda, zostaje "harmonogram gotowy"
            // bez meczów, a ponowne wyczyszczenie po prostu dokończy cofnięcie.
            await dbContext.RemoveMatchesAsync(matches, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (tournament.Status == "ScheduleGenerated")
            {
                var reverted = await tournamentModule.RevertScheduleGenerationAsync(request.TournamentId, cancellationToken);
                if (!reverted)
                    return Result.Failure<ClearScheduleResponse>(Error.Conflict("Status", "Matches were removed, but the tournament stage could not be reverted — try again"));
            }

            return Result.Success(new ClearScheduleResponse(matches.Count));
        }
    }
}
