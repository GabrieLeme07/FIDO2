using WebAPI.Infrastructure;

namespace WebAPI.IoC;

public static class DependencyContainer
{
    public static IServiceCollection RegisterService(this IServiceCollection services)
    {
        services.AddSingleton<IPassKeysDbContext, PassKeysDbContext>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}