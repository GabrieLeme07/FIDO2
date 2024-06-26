using WebAPI.Infrastructure;
using WebAPI.Services;

namespace WebAPI.IoC;

public static class DependencyContainer
{
    public static IServiceCollection RegisterService(this IServiceCollection services)
    {
        services.AddSingleton<IPassKeysDbContext, PassKeysDbContext>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<TokenService>();
        services.AddScoped<OtpService>();
        services.AddScoped<EmailService>();
        return services;
    }
}