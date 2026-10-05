using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournamentPublicSettings
{
    // Istniał tylko PUT /public-settings — formularz organizatora nie miał skąd wczytać obecnych wartości.
    public sealed record GetTournamentPublicSettingsQuery(Guid TournamentId) : IQuery<TournamentPublicSettingsDto>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
