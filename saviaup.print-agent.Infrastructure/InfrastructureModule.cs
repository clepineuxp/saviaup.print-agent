using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SaviaUp.PrintAgent.Domain.Options;
using SaviaUp.PrintAgent.Domain.Ports;
using SaviaUp.PrintAgent.Infrastructure.Backend;
using SaviaUp.PrintAgent.Infrastructure.Device;
using SaviaUp.PrintAgent.Infrastructure.Persistence;
using SaviaUp.PrintAgent.Infrastructure.Printing;

namespace SaviaUp.PrintAgent.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .Validate(x => Uri.TryCreate(x.BackendUrl, UriKind.Absolute, out _), "PrintAgent:BackendUrl must be absolute.")
            .Validate(x => x.PollingIntervalSeconds >= 5 && x.HeartbeatIntervalSeconds >= 10, "Agent intervals are too short.")
            .ValidateOnStart();
        services.AddSingleton<AgentPathResolver>();
        services.AddDbContext<LocalQueueDbContext>((provider, options) =>
        {
            var paths = provider.GetRequiredService<AgentPathResolver>();
            options.UseSqlite($"Data Source={paths.DatabasePath}");
        });
        services.AddScoped<ILocalPrintQueue, SqlitePrintQueue>();
        services.AddSingleton<IDeviceCredentialStore, ProtectedDeviceCredentialStore>();
        services.AddSingleton<IDeviceIdentity, WindowsDeviceIdentity>();
        services.AddSingleton<IInstalledPrinterDiscovery, WindowsPrinterDiscovery>();
        services.AddSingleton<ITicketRenderer, EscPosTicketRenderer>();
        services.AddSingleton<IPrinterDriver, WindowsSpoolerPrinterDriver>();
        services.AddSingleton<IPrinterDriver, NetworkEscPosPrinterDriver>();
        services.AddScoped<AuthenticatedBackendHandler>();
        services.AddHttpClient<IBackendClient, BackendClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<AgentOptions>>().Value;
            client.BaseAddress = new Uri(options.BackendUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<AuthenticatedBackendHandler>();
        services.AddSingleton<IAgentDiscoveryConnection, SignalRAgentDiscoveryConnection>();
        services.AddSingleton<IPrintNotificationConnection, SignalRPrintNotificationConnection>();
        return services;
    }
}
