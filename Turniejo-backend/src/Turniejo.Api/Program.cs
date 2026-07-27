using Turniejo.Api.Middleware;
using Turniejo.Auth.Infrastructure.Extensions;
using Turniejo.Matches.Infrastructure.Extensions;
using Turniejo.Notifications.Infrastructure.Extensions;
using Turniejo.SharedKernel.Behaviors;
using Turniejo.Teams.Infrastructure.Extensions;
using Turniejo.Tournaments.Infrastructure.Extensions;
using Turniejo.Users.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// Auth
builder.Services.AddAuthModule(builder.Configuration);

// Moduły
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddTournamentsModule(builder.Configuration);
builder.Services.AddTeamsModule(builder.Configuration);
builder.Services.AddMatchesModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Turniejo.Users.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Tournaments.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Teams.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Matches.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Notifications.Application.AssemblyReference).Assembly
    );

    cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapGet("/api/me", (Turniejo.SharedKernel.Contracts.ICurrentUserContext ctx) =>
{
    return Results.Ok(new
    {
        UserId = ctx.UserId,
        Email = ctx.Email,
        IsAuthenticated = ctx.IsAuthenticated
    });
}).RequireAuthorization();

app.Run();