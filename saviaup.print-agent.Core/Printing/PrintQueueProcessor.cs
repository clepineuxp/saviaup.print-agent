using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Entities;
using SaviaUp.PrintAgent.Domain.Options;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Core.Printing;

public sealed class PrintQueueProcessor(
    ILocalPrintQueue queue,
    IEnumerable<IPrinterDriver> drivers,
    ITicketRenderer renderer,
    IBackendClient backend,
    IOptions<AgentOptions> configuredOptions,
    TimeProvider timeProvider,
    ILogger<PrintQueueProcessor> logger) : IPrintQueueProcessor
{
    private AgentOptions Options => configuredOptions.Value;

    public Task<bool> IngestAsync(RemotePrintJob job, CancellationToken cancellationToken)
        => queue.EnqueueIfNewAsync(job, timeProvider.GetUtcNow(), cancellationToken);

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var job = await queue.GetNextAsync(timeProvider.GetUtcNow(), Math.Max(1, Options.MaximumAttempts), cancellationToken);
        if (job is null) return false;
        var now = timeProvider.GetUtcNow();
        await queue.MarkProcessingAsync(job.Id, now, cancellationToken);
        await SyncRemoteStatusesAsync(cancellationToken);

        if (await queue.IsCancelledAsync(job.Id, cancellationToken)) return true;

        var rendered = renderer.Render(job.PayloadJson, job.PaperWidth);
        if (!rendered.IsSuccess)
        {
            await FailAsync(job, rendered.Error!.Message, cancellationToken);
            return true;
        }

        var driver = drivers.FirstOrDefault(x => x.CanHandle(job.ConnectionType));
        if (driver is null)
        {
            await FailAsync(job, $"No printer driver supports {job.ConnectionType}.", cancellationToken);
            return true;
        }

        var target = new PrinterTarget(job.PrinterId, job.PrinterName, job.ConnectionType,
            job.LocalPrinterName, job.IpAddress, job.Port, job.PaperWidth);
        var result = await driver.PrintAsync(target, rendered.Value!, cancellationToken);
        if (!result.IsSuccess)
        {
            await FailAsync(job, result.Error!.Message, cancellationToken);
            return true;
        }

        await queue.MarkPrintedAsync(job.Id, timeProvider.GetUtcNow(), cancellationToken);
        await SyncRemoteStatusesAsync(cancellationToken);
        return true;
    }

    public async Task SyncRemoteStatusesAsync(CancellationToken cancellationToken)
    {
        var updates = await queue.GetPendingRemoteUpdatesAsync(100, cancellationToken);
        foreach (var job in updates)
        {
            try
            {
                await backend.ReportStatusAsync(job.PrintJobId,
                    new PrintJobStatusUpdate(job.PendingRemoteStatus!, job.PendingRemoteError), cancellationToken);
                await queue.MarkRemoteUpdateSentAsync(job.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Could not synchronize status for print job {PrintJobId}.", job.PrintJobId);
                break;
            }
        }
    }

    private async Task FailAsync(LocalPrintJob job, string error, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, job.Attempts);
        var initial = Math.Max(1, Options.InitialRetryDelaySeconds);
        var maximum = Math.Max(initial, Options.MaximumRetryDelaySeconds);
        var delay = Math.Min(maximum, initial * Math.Pow(2, Math.Min(attempts - 1, 10)));
        await queue.MarkFailedAsync(job.Id, error, timeProvider.GetUtcNow().AddSeconds(delay), cancellationToken);
        await SyncRemoteStatusesAsync(cancellationToken);
        logger.LogWarning("Print job {PrintJobId} failed on attempt {Attempt}: {Reason}", job.PrintJobId, attempts, error);
    }
}
