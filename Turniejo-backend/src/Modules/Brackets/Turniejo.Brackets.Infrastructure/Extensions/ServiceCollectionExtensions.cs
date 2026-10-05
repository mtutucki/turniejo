using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.GenerateBracket;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Infrastructure.Persistence;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Brackets.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBracketsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BracketsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "brackets");
                    npgsql.MigrationsAssembly(typeof(BracketsDbContext).Assembly.FullName);
                }));

        services.AddScoped<IBracketsDbContext>(sp => sp.GetRequiredService<BracketsDbContext>());

        services.AddScoped<BracketAdvancer>();
        services.AddScoped<IBracketModule, BracketModule>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddValidatorsFromAssemblyContaining<GenerateBracketCommandValidator>();

        return services;
    }
}
