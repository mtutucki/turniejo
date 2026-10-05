using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.ImportSquad
{
    // Kopia składu z innego zgłoszenia tej samej drużyny: nowe wpisy TournamentPlayer wskazują na te same
    // profile Player. Wszystko w jednym SaveChanges — albo wchodzi cała wybrana lista, albo nic.
    public sealed record ImportSquadCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid SourceTournamentTeamId,
        IReadOnlyList<ImportSquadPlayer> Players) : ICommand<ImportSquadResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => PlayerRoles.CoachOnly;
    }

    public sealed record ImportSquadPlayer(Guid PlayerId, int? ShirtNumber, PlayerPosition? Position);
}
