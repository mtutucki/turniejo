using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Api.Extensions;
using Turniejo.Users.Application.Users.DeleteMyAccount;
using Turniejo.Users.Application.Users.ExportMyData;

namespace Turniejo.Api.Controllers
{
    // Prawa z RODO dla zalogowanego usera: eksport własnych danych i usunięcie konta.
    // GET /api/me i /api/me/roles są minimal API w Program.cs.
    [Route("api/me")]
    [ApiController]
    public sealed class MeController(ISender sender) : ControllerBase
    {
        [HttpGet("export")]
        public async Task<IActionResult> ExportMyData(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ExportMyDataQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMyAccount(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new DeleteMyAccountCommand(), cancellationToken);
            return result.ToActionResult();
        }
    }
}
