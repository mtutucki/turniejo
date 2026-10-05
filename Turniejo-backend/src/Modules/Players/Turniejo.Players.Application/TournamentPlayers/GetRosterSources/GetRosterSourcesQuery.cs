using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.GetRosterSources
{
    // Te same role co edycja składu (Organizer turnieju / Coach drużyny) — lista służy wyłącznie do
    // kopiowania składu, a zawiera daty urodzenia zawodników z innych turniejów tej drużyny.
    public sealed record GetRosterSourcesQuery(Guid TournamentId, Guid TournamentTeamId)
        : IQuery<IReadOnlyList<RosterSourceDto>>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => PlayerRoles.CoachOnly;
    }
}
