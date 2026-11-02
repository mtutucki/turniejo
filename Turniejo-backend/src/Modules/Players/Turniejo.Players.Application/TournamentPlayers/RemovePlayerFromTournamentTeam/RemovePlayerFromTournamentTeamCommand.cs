using Turniejo.Players.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.RemovePlayerFromTournamentTeam
{
    // Zawodnik bez goli i kartek znika ze składu całkiem. Z wydarzeniami w meczach dostaje status Removed,
    // żeby statystyki i przebieg meczów dalej wskazywały na niego.
    public sealed record RemovePlayerFromTournamentTeamCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid TournamentPlayerId) : ICommand<RemovePlayerFromTournamentTeamResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => PlayerRoles.CoachOnly;
    }
}
