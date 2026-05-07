using Microsoft.Extensions.DependencyInjection;
using TechHub.Background.Workers;

namespace TechHub.Background.Extensions;

public static class BoardWorkerExtensions
{
    public static IServiceCollection AddBoardWorkers(this IServiceCollection services)
    {
        services.AddHostedService<BoardSyncWorker>();
        return services;
    }
}
