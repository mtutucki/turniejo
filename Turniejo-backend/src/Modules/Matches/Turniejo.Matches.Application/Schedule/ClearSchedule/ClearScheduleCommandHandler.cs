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
        IUserModule userModule,
        IBracketModule bracketModule) : ICommandHandler<ClearScheduleCommand, ClearScheduleResponse>
    {
        private static readonly MatchStatus[] StartedStatuses = [MatchStatus.Live, MatchStatus.Paused, MatchStatus.Finished];
        private static readonly StageType[] BracketStages = [StageType.Knockout, StageType.ThirdPlace, StageType.Final];

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

            // Mecze pucharu należą do drabinki (węzły wskazują na nie po MatchId) — usunięcie zostawiłoby
            // drabinkę z odwołaniami do nieistniejących meczów, a ponowne generowanie nie miałoby czego
            // rozstawić. Mecze pucharu zostają i dostaną nowe terminy przy generowaniu; w pucharze bez grup
            // nic nie usuwamy (jak dotąd), w pozostałych formatach znika reszta meczów.
            var toRemove = tournament.Format == "Knockout"
                ? []
                : matches.Where(m => !BracketStages.Contains(m.StageType)).ToList();

            // Sędzia zachowałby rolę Referee w usuniętym meczu (UserRoleAssignment ze scope Match).
            foreach (var match in toRemove.Where(m => m.RefereeId.HasValue))
            {
                await userModule.RevokeRoleAsync(
                    match.RefereeId!.Value, nameof(UserRole.Referee), nameof(RoleScopeType.Match), match.Id, cancellationToken);
            }

            // Najpierw mecze, potem etap: gdy cofnięcie etapu się nie uda, zostaje "harmonogram gotowy"
            // bez meczów, a ponowne wyczyszczenie po prostu dokończy cofnięcie.
            if (toRemove.Count > 0)
            {
                await dbContext.RemoveMatchesAsync(toRemove, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Terminy zaplanowane dla kolejnych rund też przestają obowiązywać.
            await bracketModule.ClearPlannedSlotsAsync(request.TournamentId, cancellationToken);

            if (tournament.Status == "ScheduleGenerated")
            {
                var reverted = await tournamentModule.RevertScheduleGenerationAsync(request.TournamentId, cancellationToken);
                if (!reverted)
                    return Result.Failure<ClearScheduleResponse>(toRemove.Count > 0
                        ? Error.Conflict("Status", "Matches were removed, but the tournament stage could not be reverted — try again")
                        : Error.Conflict("Status", "The tournament stage could not be reverted — try again"));
            }

            return Result.Success(new ClearScheduleResponse(toRemove.Count));
        }
    }
}
