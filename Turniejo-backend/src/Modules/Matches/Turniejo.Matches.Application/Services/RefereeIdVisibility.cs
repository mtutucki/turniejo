using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Services
{
    // Terminarz i szczegóły meczu czytają wszyscy członkowie turnieju (trenerzy, kibice), a RefereeId to
    // identyfikator konta użytkownika. Pełny widok ma organizator; sędzia widzi tylko własne przypisania.
    public sealed class RefereeIdVisibility(IAuthorizationService authorizationService, ICurrentUserContext currentUserContext)
    {
        public async Task<Func<Guid?, Guid?>> GetFilterAsync(Guid tournamentId, CancellationToken cancellationToken)
        {
            var isOrganizer = await authorizationService.HasAnyRoleAsync(
                currentUserContext.UserId, nameof(RoleScopeType.Tournament), tournamentId, [nameof(UserRole.Organizer)], cancellationToken);

            if (isOrganizer)
                return refereeId => refereeId;

            var userId = currentUserContext.UserId;
            return refereeId => refereeId == userId ? refereeId : null;
        }
    }
}
