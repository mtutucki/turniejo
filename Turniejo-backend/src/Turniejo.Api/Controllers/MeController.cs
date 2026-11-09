using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Turniejo.Api.Extensions;
using Turniejo.Users.Application.Users.ChangeMyPassword;
using Turniejo.Users.Application.Users.DeleteMyAccount;
using Turniejo.Users.Application.Users.ExportMyData;
using Turniejo.Users.Application.Users.GetMyProfile;
using Turniejo.Users.Application.Users.MarkReleaseSeen;
using Turniejo.Users.Application.Users.UpdateMyProfile;

namespace Turniejo.Api.Controllers
{
    // Konto zalogowanego usera: profil, zmiana hasła i prawa z RODO (eksport danych, usunięcie konta).
    // GET /api/me i /api/me/roles są minimal API w Program.cs.
    [Route("api/me")]
    [ApiController]
    public sealed class MeController(ISender sender) : ControllerBase
    {
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMyProfileQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        // Zamknięcie okna "Co nowego" (wersja X.Y.Z). Starsza niż zapisana nic nie zmienia.
        [HttpPut("seen-release")]
        public async Task<IActionResult> MarkReleaseSeen([FromBody] MarkReleaseSeenCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        // Limit per user: każda próba to logowanie do Keycloaka, więc bez niego endpoint służyłby
        // do zgadywania hasła z przejętej sesji.
        [HttpPost("password")]
        [EnableRateLimiting(RateLimitingExtensions.ChangePassword)]
        public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeMyPasswordCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportMyData(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ExportMyDataQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMyAccount(CancellationToken cancellationToken, [FromQuery] bool deleteOrganization = false)
        {
            var result = await sender.Send(new DeleteMyAccountCommand(deleteOrganization), cancellationToken);
            return result.ToActionResult();
        }
    }
}
