using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Entities;
using SaviaUp.PrintAgent.Infrastructure.Persistence;

namespace SaviaUp.PrintAgent.IntegrationTests;

public sealed class SqlitePrintQueueTests
{
    [Fact]
    public async Task EnqueueIfNewAsync_IsIdempotentByPrintJobId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LocalQueueDbContext>().UseSqlite(connection).Options;
        await using var context = new LocalQueueDbContext(options);
        var queue = new SqlitePrintQueue(context);
        await queue.InitializeAsync(default);
        var job = Job(Guid.NewGuid());

        Assert.True(await queue.EnqueueIfNewAsync(job, DateTimeOffset.UtcNow, default));
        Assert.False(await queue.EnqueueIfNewAsync(job, DateTimeOffset.UtcNow, default));
        Assert.Equal(1, await context.PrintJobs.CountAsync());
    }

    [Fact]
    public async Task RecoverInterruptedAsync_ReturnsProcessingJobToPending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LocalQueueDbContext>().UseSqlite(connection).Options;
        await using var context = new LocalQueueDbContext(options);
        var queue = new SqlitePrintQueue(context);
        await queue.InitializeAsync(default);
        var job = Job(Guid.NewGuid());
        await queue.EnqueueIfNewAsync(job, DateTimeOffset.UtcNow, default);
        var stored = await context.PrintJobs.SingleAsync();
        stored.Status = LocalPrintJobStatuses.Processing;
        await context.SaveChangesAsync();

        await queue.RecoverInterruptedAsync(default);

        Assert.Equal(LocalPrintJobStatuses.Pending, (await context.PrintJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task PrintedJob_IsNeverSelectedOrReinsertedAfterDuplicateNotification()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LocalQueueDbContext>().UseSqlite(connection).Options;
        await using var context = new LocalQueueDbContext(options);
        var queue = new SqlitePrintQueue(context);
        await queue.InitializeAsync(default);
        var remote = Job(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        await queue.EnqueueIfNewAsync(remote, now, default);
        var stored = await context.PrintJobs.SingleAsync();
        await queue.MarkPrintedAsync(stored.Id, now, default);

        Assert.False(await queue.EnqueueIfNewAsync(remote, now.AddMinutes(1), default));
        Assert.Null(await queue.GetNextAsync(now.AddMinutes(1), 10, default));
        Assert.Equal(1, await context.PrintJobs.CountAsync());
    }

    [Fact]
    public async Task CancelAsync_RemovesPendingJobFromTheLocalQueue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LocalQueueDbContext>().UseSqlite(connection).Options;
        await using var context = new LocalQueueDbContext(options);
        var queue = new SqlitePrintQueue(context);
        await queue.InitializeAsync(default);
        var remote = Job(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        await queue.EnqueueIfNewAsync(remote, now, default);

        await queue.CancelAsync(remote.Id, default);

        var stored = await context.PrintJobs.SingleAsync();
        Assert.Equal(LocalPrintJobStatuses.Cancelled, stored.Status);
        Assert.True(await queue.IsCancelledAsync(stored.Id, default));
        Assert.Null(await queue.GetNextAsync(now.AddMinutes(1), 10, default));
    }

    private static RemotePrintJob Job(Guid id) => new(
        id, Guid.NewGuid(), "Kitchen", "ESC_POS_NETWORK", null, "127.0.0.1", 9100, 80,
        "{\"documentType\":\"KitchenOrder\",\"orderNumber\":\"CMD-1\",\"waiter\":\"Ana\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"items\":[]}",
        "PENDING", false);
}
