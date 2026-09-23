using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaviaUp.PrintAgent.Domain.Options;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Backend;

public sealed class SignalRPrintNotificationConnection(
    IOptions<AgentOptions> configuredOptions,
    IDeviceCredentialStore credentials,
    ILogger<SignalRPrintNotificationConnection> logger) : IPrintNotificationConnection
{
    private sealed record JobNotification(Guid PrintJobId);

    public async Task RunAsync(
        Func<Guid, Task> onJobAvailable,
        Func<Guid, Task> onJobCancelled,
        Func<Task> onPrinterDiscoveryRequested,
        CancellationToken cancellationToken)
    {
        var token = await credentials.ReadTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return;
        var url = configuredOptions.Value.BackendUrl.TrimEnd('/') + "/hubs/printing";
        await using var connection = new HubConnectionBuilder()
            .WithUrl(url, options => options.AccessTokenProvider = () => credentials.ReadTokenAsync(CancellationToken.None))
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .Build();
        connection.On<JobNotification>("OnPrintJobAvailable", notification => onJobAvailable(notification.PrintJobId));
        connection.On<JobNotification>("OnPrintJobCancelled", notification => onJobCancelled(notification.PrintJobId));
        connection.On("OnPrinterDiscoveryRequested", onPrinterDiscoveryRequested);
        connection.Reconnecting += exception =>
        {
            logger.LogWarning("Printing realtime connection interrupted; pending jobs will continue through polling.");
            return Task.CompletedTask;
        };
        await connection.StartAsync(cancellationToken);
        logger.LogInformation("Printing realtime connection established.");
        try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
