//using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechHub.Core.Configuration;
using TechHub.Service.Interface;
using TechHub.Service.Repository;
using TechHub.Service.Service;

namespace TechHub.Service.Extensions;

public static class BoardServiceExtensions
{
    public static IServiceCollection AddBoardServices(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // Configuration
        services.Configure<MongoDbSettings>(
            configuration.GetSection(MongoDbSettings.SectionName));

        services.Configure<RabbitMQSettings>(
            configuration.GetSection(RabbitMQSettings.SectionName));

        // Repository
        services.AddSingleton<IBoardSessionRepository, BoardSessionRepository>();
        services.AddSingleton<IGroupContentBoardRepository, GroupContentBoardRepository>();

        // Services
        services.AddSingleton<IBoardPublisherService, BoardPublisherService>();
        services.AddScoped<IBoardSessionService, BoardSessionService>();
        services.AddSingleton<IGroupContentBoardPublisherService, GroupContentBoardPublisherService>();
        services.AddScoped<IGroupContentBoardSessionService, GroupContentBoardSessionService>();

        return services;
    }
}
