using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.SetGoalPlayers
{
    /// <summary>
    /// Strzelec i asysta już zapisanego gola (np. wynik wpisany bez zegara albo gol szybki bez zawodnika).
    /// Wynik się nie zmienia. Sędzia w trakcie meczu i w oknie poprawek, organizator także później.
    /// </summary>
    public sealed record SetGoalPlayersCommand(
        Guid TournamentId,
        Guid MatchId,
        Guid MatchEventId,
        Guid? PlayerId,
        Guid? AssistPlayerId) : ICommand<SetGoalPlayersResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}
