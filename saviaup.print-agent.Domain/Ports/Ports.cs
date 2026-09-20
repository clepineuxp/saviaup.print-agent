using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Entities;
using SaviaUp.PrintAgent.Shared.Results;

namespace SaviaUp.PrintAgent.Domain.Ports;

public interface ILocalPrintQueue
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task RecoverInterruptedAsync(CancellationToken cancellationToken);
    Task<bool> EnqueueIfNewAsync(RemotePrintJob job, DateTimeOffset now, CancellationToken cancellationToken);
    Task<LocalPrintJob?> GetNextAsync(DateTimeOffset now, int maximumAttempts, CancellationToken cancellationToken);
    Task MarkProcessingAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task MarkPrintedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid id, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LocalPrintJob>> GetPendingRemoteUpdatesAsync(int limit, CancellationToken cancellationToken);
    Task MarkRemoteUpdateSentAsync(Guid id, CancellationToken cancellationToken);
}

public interface IBackendClient
{
    Task<IReadOnlyCollection<RemotePrintJob>> GetPendingJobsAsync(CancellationToken cancellationToken);
    Task ReportStatusAsync(Guid printJobId, PrintJobStatusUpdate update, CancellationToken cancellationToken);
    Task HeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken);
    Task SyncPrintersAsync(IReadOnlyCollection<DiscoveredPrinter> printers, CancellationToken cancellationToken);
}

public interface IAgentDiscoveryConnection
{
    Task<PairAgentResponse?> WaitForPairingAsync(DiscoverAgentRequest request, CancellationToken cancellationToken);
}

public interface IDeviceCredentialStore
{
    Task<string?> ReadTokenAsync(CancellationToken cancellationToken);
    Task WriteTokenAsync(string token, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public interface IDeviceIdentity
{
    string DeviceIdentifier { get; }
    string Hostname { get; }
    string OperatingSystem { get; }
    string Version { get; }
    string? LocalIpAddress { get; }
}

public interface IInstalledPrinterDiscovery
{
    Task<IReadOnlyCollection<DiscoveredPrinter>> GetInstalledAsync(CancellationToken cancellationToken);
}

public interface IPrintNotificationConnection
{
    Task RunAsync(
        Func<Guid, Task> onJobAvailable,
        Func<Task> onPrinterDiscoveryRequested,
        CancellationToken cancellationToken);
}

public interface IPrinterDriver
{
    bool CanHandle(string connectionType);
    Task<Result> PrintAsync(PrinterTarget printer, ReadOnlyMemory<byte> document, CancellationToken cancellationToken);
}

public interface ITicketRenderer
{
    Result<byte[]> Render(string payloadJson, int paperWidth);
}

public interface IPrintQueueProcessor
{
    Task<bool> IngestAsync(RemotePrintJob job, CancellationToken cancellationToken);
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
    Task SyncRemoteStatusesAsync(CancellationToken cancellationToken);
}
