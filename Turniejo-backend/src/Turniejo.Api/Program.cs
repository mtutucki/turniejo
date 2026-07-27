using Turniejo.Api.Middleware;
using Turniejo.Matches.Infrastructure.Extensions;
using Turniejo.Notifications.Infrastructure.Extensions;
using Turniejo.SharedKernel.Behaviors;
using Turniejo.Teams.Infrastructure.Extensions;
using Turniejo.Tournaments.Infrastructure.Extensions;
using Turniejo.Users.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddTournamentsModule(builder.Configuration);
builder.Services.AddTeamsModule(builder.Configuration);
builder.Services.AddMatchesModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

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



builder.Services.AddHostedService<Turniejo.Api.BackgroundServices.OutboxProcessor>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapHealthChecks("/health");

app.Run();