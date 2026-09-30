using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.CreateBracketMatch
{
    // Mecz drabinki powstawał z terminem "teraz" i bez boiska — w terminarzu i na portalu kibica wisiał
    // jako zaplanowany na moment utworzenia. Teraz dostaje propozycję terminu: nie wcześniej niż po fazie
    // grupowej i po ostatnich meczach obu drużyn, na boisku, które najwcześniej się zwalnia. Organizator
    // może go potem przesunąć w terminarzu (UpdateMatchSchedule).
    internal sealed class CreateBracketMatchCommandHandler(IMatchesDbContext dbContext, ITournamentModule tournamentModule)
        : ICommandHandler<CreateBracketMatchCommand, CreateBracketMatchResponse>
    {
        public async Task<Result<CreateBracketMatchResponse>> Handle(CreateBracketMatchCommand request, CancellationToken cancellationToken)
        {
            var (pitchId, scheduledAt) = await ProposeSlotAsync(request, cancellationToken);

            var result = Match.Create(
                Guid.NewGuid(), request.TournamentId, request.CategoryId, null,
                request.HomeTournamentTeamId, request.AwayTournamentTeamId, pitchId,
                scheduledAt, request.StageType);
            if (result.IsFailure)
                return Result.Failure<CreateBracketMatchResponse>(result.Error);

            await dbContext.AddMatchAsync(result.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateBracketMatchResponse(result.Value.Id));
        }

        private async Task<(Guid? PitchId, DateTimeOffset ScheduledAt)> ProposeSlotAsync(
            CreateBracketMatchCommand request, CancellationToken cancellationToken)
        {
            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            var slot = TimeSpan.FromMinutes((settings?.MatchDurationMinutes ?? 0) + (settings?.BreakDurationMinutes ?? 0));
            if (slot <= TimeSpan.Zero)
                slot = TimeSpan.FromMinutes(1);

            var matches = (await dbContext.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken))
                .Where(m => m.Status != MatchStatus.Cancelled)
                .ToList();
            DateTimeOffset End(Match m) => m.ScheduledAt + slot;

            // Puchar generowany przed turniejem: termin nie wcześniej niż start turnieju (a nie "teraz").
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var notBefore = new[]
            {
                now,
                tournament?.StartAt ?? now,
                matches.Where(m => m.StageType == StageType.Group).Select(End).DefaultIfEmpty(now).Max(),
                matches
                    .Where(m => m.HomeTournamentTeamId == request.HomeTournamentTeamId || m.AwayTournamentTeamId == request.HomeTournamentTeamId ||
                                m.HomeTournamentTeamId == request.AwayTournamentTeamId || m.AwayTournamentTeamId == request.AwayTournamentTeamId)
                    .Select(End).DefaultIfEmpty(now).Max()
            }.Max();

            var pitches = (await dbContext.GetPitchesByTournamentIdAsync(request.TournamentId, cancellationToken))
                .Where(p => p.IsActive)
                .ToList();
            if (pitches.Count == 0)
                return (null, notBefore);

            return pitches
                .Select(p => (PitchId: (Guid?)p.Id, ScheduledAt: matches
                    .Where(m => m.PitchId == p.Id)
                    .Select(End)
                    .Append(notBefore)
                    .Max()))
                .OrderBy(x => x.ScheduledAt)
                .First();
        }
    }
}
