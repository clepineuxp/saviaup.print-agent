using Microsoft.EntityFrameworkCore;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Entities;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Persistence;

public sealed class SqlitePrintQueue(LocalQueueDbContext dbContext) : ILocalPrintQueue
{
    public Task InitializeAsync(CancellationToken cancellationToken)
        => dbContext.Database.EnsureCreatedAsync(cancellationToken);

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        var interrupted = await dbContext.PrintJobs.Where(x => x.Status == LocalPrintJobStatuses.Processing).ToListAsync(cancellationToken);
        foreach (var job in interrupted)
        {
            job.Status = LocalPrintJobStatuses.Pending;
            job.NextAttemptAtUtc = null;
            job.LastError = "Recovered after an interrupted agent execution.";
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> EnqueueIfNewAsync(RemotePrintJob job, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await dbContext.PrintJobs.AnyAsync(x => x.PrintJobId == job.Id, cancellationToken)) return false;
        dbContext.PrintJobs.Add(new LocalPrintJob
        {
            Id = Guid.NewGuid(),
            PrintJobId = job.Id,
            PrinterId = job.PrinterId,
            PrinterName = job.PrinterName,
            ConnectionType = job.PrinterConnectionType,
            LocalPrinterName = job.LocalPrinterName,
            IpAddress = job.PrinterIpAddress,
            Port = job.PrinterPort,
            PaperWidth = job.PaperWidth,
            PayloadJson = job.PayloadJson,
            Status = LocalPrintJobStatuses.Pending,
            CreatedAtUtc = now.UtcDateTime
        });
        try { await dbContext.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException) { dbContext.ChangeTracker.Clear(); return false; }
    }

    public Task<LocalPrintJob?> GetNextAsync(DateTimeOffset now, int maximumAttempts, CancellationToken cancellationToken)
        => dbContext.PrintJobs.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(
            x => x.Attempts < maximumAttempts
                && (x.Status == LocalPrintJobStatuses.Pending || x.Status == LocalPrintJobStatuses.Failed)
                && (!x.NextAttemptAtUtc.HasValue || x.NextAttemptAtUtc <= now.UtcDateTime), cancellationToken);

    public async Task MarkProcessingAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var job = await RequiredAsync(id, cancellationToken);
        job.Status = LocalPrintJobStatuses.Processing;
        job.Attempts++;
        job.ProcessedAtUtc = now.UtcDateTime;
        job.PendingRemoteStatus = "PROCESSING";
        job.PendingRemoteError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPrintedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var job = await RequiredAsync(id, cancellationToken);
        job.Status = LocalPrintJobStatuses.Printed;
        job.ProcessedAtUtc = now.UtcDateTime;
        job.NextAttemptAtUtc = null;
        job.LastError = null;
        job.PendingRemoteStatus = "PRINTED";
        job.PendingRemoteError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid id, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
    {
        var job = await RequiredAsync(id, cancellationToken);
        job.Status = LocalPrintJobStatuses.Failed;
        job.LastError = error.Length > 2000 ? error[..2000] : error;
        job.NextAttemptAtUtc = nextAttemptAt.UtcDateTime;
        job.PendingRemoteStatus = "FAILED";
        job.PendingRemoteError = job.LastError;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LocalPrintJob>> GetPendingRemoteUpdatesAsync(int limit, CancellationToken cancellationToken)
        => await dbContext.PrintJobs.AsNoTracking().Where(x => x.PendingRemoteStatus != null)
            .OrderBy(x => x.ProcessedAtUtc).Take(limit).ToListAsync(cancellationToken);

    public async Task MarkRemoteUpdateSentAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await RequiredAsync(id, cancellationToken);
        job.PendingRemoteStatus = null;
        job.PendingRemoteError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<LocalPrintJob> RequiredAsync(Guid id, CancellationToken cancellationToken)
        => await dbContext.PrintJobs.FirstAsync(x => x.Id == id, cancellationToken);
}
