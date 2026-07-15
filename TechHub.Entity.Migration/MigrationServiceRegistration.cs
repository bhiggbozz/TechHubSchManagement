using Microsoft.Extensions.DependencyInjection;

namespace TechHub.Entity.Migration;

public static class MigrationServiceRegistration
{
    public static IServiceCollection AddDatabaseMigration(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
