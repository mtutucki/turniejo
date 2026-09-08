using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.UpdateMatchEvent
{
    public sealed record UpdateMatchEventCommand(
        Guid TournamentId,
        Guid MatchId,
        Guid MatchEventId,
        MatchEventType Type,
        int Minute,
        int? Second,
        Guid? PlayerId,
        Guid? RelatedPlayerId) : ICommand<UpdateMatchEventResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}
