using Microsoft.Extensions.DependencyInjection;
using SaviaUp.PrintAgent.Core.Printing;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Core;

public static class CoreModule
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddScoped<IPrintQueueProcessor, PrintQueueProcessor>();
        return services;
    }
}
