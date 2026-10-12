using Turniejo.Api.Extensions;
using Turniejo.Api.Json;
using Turniejo.Api.Middleware;
using Turniejo.Auth.Infrastructure.Extensions;
using Turniejo.Brackets.Infrastructure.Extensions;
using Turniejo.Matches.Infrastructure.Extensions;
using Turniejo.Matches.Infrastructure.Hubs;
using Turniejo.Notifications.Infrastructure.Extensions;
using Turniejo.Organizations.Infrastructure.Extensions;
using Turniejo.Players.Infrastructure.Extensions;
using Turniejo.SharedKernel.Behaviors;
using Turniejo.Standings.Infrastructure.Extensions;
using Turniejo.Teams.Infrastructure.Extensions;
using Turniejo.Tournaments.Infrastructure.Extensions;
using Turniejo.Users.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
});
builder.Services.AddTrustedProxyForwarding(builder.Configuration);
builder.Services.AddApiRateLimiting();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddSmtpEmail(builder.Configuration);

// Auth
builder.Services.AddAuthModule(builder.Configuration, builder.Environment);

// Moduły
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddTournamentsModule(builder.Configuration);
builder.Services.AddTeamsModule(builder.Configuration);
builder.Services.AddMatchesModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddOrganizationsModule(builder.Configuration);
builder.Services.AddPlayersModule(builder.Configuration);
builder.Services.AddStandingsModule(builder.Configuration);
builder.Services.AddBracketsModule(builder.Configuration);



// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Turniejo.Users.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Tournaments.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Teams.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Matches.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Notifications.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Organizations.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Players.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Standings.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Brackets.Application.AssemblyReference).Assembly,
        typeof(Turniejo.Tournaments.Application.AssemblyReference).Assembly
    );

    cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

builder.Services.AddHostedService<Turniejo.Api.BackgroundServices.OutboxProcessor>();
builder.Services.AddHostedService<Turniejo.Api.BackgroundServices.MatchReminderBackgroundService>();

var app = builder.Build();

// Pierwsze w pipeline — wszystko dalej (rate limiter, logi) ma widzieć adres klienta, nie proxy.
app.UseForwardedHeaders();

// Dostępny tylko w Development — przed produkcyjnym wdrożeniem odsłania pełną mapę API
// (włącznie z tym, co wymaga autoryzacji), więc świadomie nie wystawiamy go publicznie domyślnie.
if (app.Environment.IsDevelopment())
    app.UseSwaggerDocumentation();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");

app.MapControllers();
app.MapHub<MatchHub>("/hubs/matches");
app.MapHub<TournamentHub>("/hubs/tournaments");
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/api/me", (Turniejo.SharedKernel.Contracts.ICurrentUserContext ctx) =>
{
    return Results.Ok(new
    {
        UserId = ctx.UserId,
        Email = ctx.Email,
        IsAuthenticated = ctx.IsAuthenticated
    });
}).RequireAuthorization().WithTags("Me");

// Frontend ukrywa akcje na podstawie ról usera w konkretnym zasobie (rola + ScopeType + ScopeId),
// więc potrzebuje pełnej listy przypisań zalogowanego usera — tej samej, którą sprawdza AuthorizationBehavior.
app.MapGet("/api/me/roles", async (
    Turniejo.SharedKernel.Contracts.ICurrentUserContext ctx,
    Turniejo.SharedKernel.Contracts.IUserModule userModule,
    CancellationToken cancellationToken) =>
{
    var roles = await userModule.GetUserRolesAsync(ctx.UserId, cancellationToken);
    return Results.Ok(roles.Select(r => new { r.Role, r.ScopeType, r.ScopeId }));
}).RequireAuthorization().WithTags("Me");

await app.MigrateDatabaseIfEnabledAsync();

app.Run();

// Top-level statements generują domyślnie internal partial class Program — WebApplicationFactory<Program>
// z testów integracyjnych potrzebuje go publicznego, żeby w ogóle się do niego odwołać.
public partial class Program;