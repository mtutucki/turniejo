using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.CreateBracketMatch
{
    public sealed record CreateBracketMatchCommand(
        Guid TournamentId,
        Guid? CategoryId,
        Guid HomeTournamentTeamId,
        Guid AwayTournamentTeamId,
        StageType StageType,
        // Termin zaplanowany wcześniej przez generator terminarza (IBracketModule) — brak = propozycja.
        Guid? PlannedPitchId = null,
        DateTimeOffset? PlannedAt = null,
        // Sędzia wskazany przez organizatora na węźle drabinki, zanim mecz powstał.
        Guid? PlannedRefereeId = null) : ICommand<CreateBracketMatchResponse>, IPublicRequest;
}
