using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.UpdatePlayerInTournamentTeam
{
    // Lustro CreatePlayerInTournamentTeam dla edycji: profil (imię, nazwisko, data urodzenia, zdjęcie)
    // i wpis w składzie (numer, pozycja, status) w jednym SaveChanges. Osobne UpdatePlayer nie zna
    // kategorii drużyny, więc pozwalałoby zmienić rocznik zawodnika poza limit już po dodaniu do składu.
    public sealed record UpdatePlayerInTournamentTeamCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid TournamentPlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        string? PhotoUrl,
        int? ShirtNumber,
        PlayerPosition? Position,
        TournamentPlayerStatus Status) : ICommand<UpdatePlayerInTournamentTeamResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;

        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => PlayerRoles.CoachOnly;
    }
}
