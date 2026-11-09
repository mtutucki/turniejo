using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournament
{
    public sealed record UpdateTournamentDetailsCommand(
        Guid TournamentId,
        string Name,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        int TeamLimit,
        string? Description,
        // UpdatedAt turnieju z chwili otwarcia formularza; inny w bazie = ktoś zapisał w międzyczasie.
        DateTimeOffset? ExpectedUpdatedAt = null
    ) : ICommand<UpdateTournamentDetailsResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
