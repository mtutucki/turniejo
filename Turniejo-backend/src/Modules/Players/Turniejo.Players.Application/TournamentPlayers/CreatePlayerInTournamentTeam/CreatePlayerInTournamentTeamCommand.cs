using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.CreatePlayerInTournamentTeam
{
    // Nowy profil + wpis do składu w jednym SaveChanges. Osobne CreatePlayer + AssignPlayer zostawiały
    // osierocony profil, gdy przypisanie się nie powiodło (rocznik, zajęty numer, wyścig z innym edytującym).
    public sealed record CreatePlayerInTournamentTeamCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        PlayerPosition DefaultPosition,
        string? PhotoUrl,
        int? ShirtNumber,
        PlayerPosition? Position) : ICommand<CreatePlayerInTournamentTeamResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => PlayerRoles.CoachOnly;
    }
}
