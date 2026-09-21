using Microsoft.Extensions.Options;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Options;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Worker;

public sealed class PrintAgentWorker(
    IServiceScopeFactory scopeFactory,
    IDeviceCredentialStore credentials,
    IDeviceIdentity device,
    IAgentDiscoveryConnection discovery,
    IInstalledPrinterDiscovery printerDiscovery,
    IPrintNotificationConnection realtime,
    IOptions<AgentOptions> configuredOptions,
    ILogger<PrintAgentWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly SemaphoreSlim _printerSync = new(1, 1);
    private AgentOptions Options => configuredOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeQueueAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await EnsurePairedAsync(stoppingToken))
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                continue;
            }

            await SyncPrintersAsync(stoppingToken);
            Wake();
            using var cycle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var loops = new[]
            {
                RunRealtimeLoopAsync(cycle.Token),
                RunProcessingLoopAsync(cycle.Token),
                RunHeartbeatLoopAsync(cycle.Token)
            };
            await Task.WhenAny(loops);
            await cycle.CancelAsync();
            try { await Task.WhenAll(loops); }
            catch (OperationCanceledException) when (cycle.IsCancellationRequested) { }
        }
    }

    private async Task InitializeQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<ILocalPrintQueue>();
        await queue.InitializeAsync(cancellationToken);
        await queue.RecoverInterruptedAsync(cancellationToken);
    }

    private async Task<bool> EnsurePairedAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(await credentials.ReadTokenAsync(cancellationToken))) return true;
        try
        {
            var response = await discovery.WaitForPairingAsync(new DiscoverAgentRequest(
                device.DeviceIdentifier, device.Hostname,
                device.OperatingSystem, device.Version, device.LocalIpAddress), cancellationToken);
            if (response is null) return false;
            await credentials.WriteTokenAsync(response.DeviceToken, cancellationToken);
            logger.LogInformation("Print agent paired successfully as {AgentId}.", response.AgentId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not connect the print agent for linking.");
            return false;
        }
    }

    private async Task RunRealtimeLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await realtime.RunAsync(
                    _ => { Wake(); return Task.CompletedTask; },
                    async printJobId =>
                    {
                        using var scope = scopeFactory.CreateScope();
                        var queue = scope.ServiceProvider.GetRequiredService<ILocalPrintQueue>();
                        await queue.CancelAsync(printJobId, cancellationToken);
                        Wake();
                    },
                    () => SyncPrintersAsync(cancellationToken),
                    cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await InvalidateCredentialAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Realtime connection failed; retrying while REST polling remains active.");
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }
    }

    private async Task RunProcessingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var backend = scope.ServiceProvider.GetRequiredService<IBackendClient>();
                var processor = scope.ServiceProvider.GetRequiredService<IPrintQueueProcessor>();
                var jobs = await backend.GetPendingJobsAsync(cancellationToken);
                foreach (var job in jobs) await processor.IngestAsync(job, cancellationToken);
                while (await processor.ProcessNextAsync(cancellationToken)) { }
                await processor.SyncRemoteStatusesAsync(cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await InvalidateCredentialAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Print queue cycle failed; local jobs remain persisted.");
            }

            var delay = Task.Delay(TimeSpan.FromSeconds(Math.Max(5, Options.PollingIntervalSeconds)), cancellationToken);
            var wake = _wake.WaitAsync(cancellationToken);
            await Task.WhenAny(delay, wake);
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var backend = scope.ServiceProvider.GetRequiredService<IBackendClient>();
                await backend.HeartbeatAsync(new HeartbeatRequest(device.Version, device.LocalIpAddress, "ONLINE"), cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await InvalidateCredentialAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Heartbeat could not reach the backend; the agent will retry.");
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, Options.HeartbeatIntervalSeconds)), cancellationToken);
        }
    }

    private async Task SyncPrintersAsync(CancellationToken cancellationToken)
    {
        await _printerSync.WaitAsync(cancellationToken);
        try
        {
            var printers = await printerDiscovery.GetInstalledAsync(cancellationToken);
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IBackendClient>().SyncPrintersAsync(printers, cancellationToken);
            logger.LogInformation("Synchronized {PrinterCount} installed printers.", printers.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Installed printers could not be synchronized.");
        }
        finally
        {
            _printerSync.Release();
        }
    }

    private void Wake()
    {
        if (_wake.CurrentCount == 0) _wake.Release();
    }

    private async Task InvalidateCredentialAsync(CancellationToken cancellationToken)
    {
        await credentials.ClearAsync(cancellationToken);
        logger.LogWarning("The print agent credential was revoked or expired. It is available to be linked again from Savia Up.");
    }
}
