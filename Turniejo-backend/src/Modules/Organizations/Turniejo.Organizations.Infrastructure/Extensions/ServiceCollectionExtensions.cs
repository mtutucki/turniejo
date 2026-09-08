using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Application.Organizations.CreateOrganization;
using Turniejo.Organizations.Application.Services;
using Turniejo.Organizations.Infrastructure.Persistence;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Organizations.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OrganizationsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "organizations");
                    npgsql.MigrationsAssembly(typeof(OrganizationsDbContext).Assembly.FullName);
                }));

        services.AddScoped<IOrganizationsDbContext>(sp =>
            sp.GetRequiredService<OrganizationsDbContext>());

        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddScoped<OrganizationProvisioner>();

        services.AddValidatorsFromAssemblyContaining<CreateOrganizationCommandValidator>();

        return services;
    }
}