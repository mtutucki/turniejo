using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.RegisterUser;
using Turniejo.Users.Infrastructure.Persistence;

namespace Turniejo.Users.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "users");
                    npgsql.MigrationsAssembly(typeof(UsersDbContext).Assembly.FullName);
                }));

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddScoped<IUsersDbContext>(sp => sp.GetRequiredService<UsersDbContext>());

        services.AddScoped<IUserModule, UserModule>();

        // Rejestracja walidatorów
        services.AddValidatorsFromAssemblyContaining<RegisterUserCommandValidator>();

        return services;
    }
}