using Turniejo.Api.Email;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Api.Extensions;

public static class EmailExtensions
{
    public static IServiceCollection AddSmtpEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
