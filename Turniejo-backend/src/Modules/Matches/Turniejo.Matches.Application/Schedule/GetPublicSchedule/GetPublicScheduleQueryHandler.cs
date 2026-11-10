using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Schedule.GetPublicSchedule
{
    internal sealed class GetPublicScheduleQueryHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicScheduleQuery, IReadOnlyList<MatchDto>>
    {
        public async Task<Result<IReadOnlyList<MatchDto>>> Handle(GetPublicScheduleQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<MatchDto>>(Error.NotFound("Tournament", request.Slug));

            var showSchedule = tournament.Settings.ShowSchedule;
            var showLive = tournament.Settings.ShowLiveMatches;
            if (!showSchedule && !showLive)
                return Result.Success<IReadOnlyList<MatchDto>>([]);

            // Przełączniki są niezależne (QA v3: PUB-003c/f): bez terminarza kibic dalej widzi mecze na żywo,
            // a bez „Meczów na żywo” trwający mecz jest w terminarzu, ale bez bieżącego wyniku.
            static bool IsLive(Match m) => m.Status is MatchStatus.Live or MatchStatus.Paused;
            var matches = (await dbContext.GetMatchesByTournamentIdAsync(tournament.Id, cancellationToken))
                .Where(m => showSchedule || IsLive(m))
                .ToList();

            // RefereeId celowo null — kto sędziuje to wewnętrzna informacja organizacyjna, nie coś
            // do publicznego ujawnienia przez stronę turnieju, tym bardziej dla meczów, które się
            // jeszcze nie odbyły.
            var dtos = matches
                .OrderBy(m => m.ScheduledAt)
                .Select(m => new MatchDto(
                    m.Id, m.TournamentId, m.CategoryId, m.GroupId, m.PitchId,
                    m.HomeTournamentTeamId, m.AwayTournamentTeamId, RefereeId: null, m.ScheduledAt,
                    m.Status.ToString(), m.StageType.ToString(),
                    !showLive && IsLive(m) ? 0 : m.HomeScore, !showLive && IsLive(m) ? 0 : m.AwayScore, m.ResultType.ToString(),
                    m.HomePenaltyScore, m.AwayPenaltyScore, AwaitingDecision: m.IsAwaitingDecision))
                .ToList();

            return Result.Success<IReadOnlyList<MatchDto>>(dtos);
        }
    }
}
