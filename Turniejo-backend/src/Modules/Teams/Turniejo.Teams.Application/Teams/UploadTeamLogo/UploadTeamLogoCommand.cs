using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.Teams.UploadTeamLogo
{
    /// <summary>
    /// Wgranie pliku z herbem. Zwraca adres, który formularz drużyny zapisuje potem jako LogoUrl
    /// (działa też przy tworzeniu drużyny, zanim ta ma Id).
    /// </summary>
    public sealed record UploadTeamLogoCommand(Guid OrganizationId, byte[] Content)
        : ICommand<UploadTeamLogoResponse>, IAuthorizedRequest
    {
        /// <summary>Frontend zmniejsza obrazek do 256 px przed wysłaniem, więc to spory zapas.</summary>
        public const int MaxBytes = 512 * 1024;

        public string ScopeType => nameof(RoleScopeType.Organization);
        public Guid ScopeId => OrganizationId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
