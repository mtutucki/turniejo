using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.PlayerGuardians.AssignGuardianToPlayer
{
    // Guardian jest przypisywany w kontekście konkretnej drużyny turniejowej (roster),
    // żeby autoryzację dało się oprzeć na realnej relacji (Organizer turnieju / Coach drużyny),
    // a nie na samej znajomości PlayerId. Samoobsługowe zaproszenia opiekunów to V2 (arkusz: "Zaproszenia V2").
    public sealed record AssignGuardianToPlayerCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid PlayerId,
        Guid UserId,
        RelationshipType RelationshipType,
        bool CanEdit,
        bool CanReceiveNotifications) : ICommand<AssignGuardianToPlayerResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => PlayerRoles.CoachOnly;
    }
}
