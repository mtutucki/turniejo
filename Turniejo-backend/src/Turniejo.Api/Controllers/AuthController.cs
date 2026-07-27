using MediatR;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Users.Application.Users.RegisterUser;
using Turniejo.Users.Application.Users.RequestPasswordReset;

namespace Turniejo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return Created($"/api/users/{result.Value.UserId}",
            new { userId = result.Value.UserId });
    }

    [HttpPost("password-reset/request")]
    public async Task<IActionResult> RequestPasswordReset(
    [FromBody] RequestPasswordResetCommand command,
    CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }
}