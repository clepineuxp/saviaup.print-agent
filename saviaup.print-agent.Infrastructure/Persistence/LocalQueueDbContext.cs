using Microsoft.EntityFrameworkCore;
using SaviaUp.PrintAgent.Domain.Entities;

namespace SaviaUp.PrintAgent.Infrastructure.Persistence;

public sealed class LocalQueueDbContext(DbContextOptions<LocalQueueDbContext> options) : DbContext(options)
{
    public DbSet<LocalPrintJob> PrintJobs => Set<LocalPrintJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<LocalPrintJob>();
        job.ToTable("print_queue");
        job.HasKey(x => x.Id);
        job.Property(x => x.Id).ValueGeneratedNever();
        job.Property(x => x.PrinterName).HasMaxLength(260).IsRequired();
        job.Property(x => x.ConnectionType).HasMaxLength(40).IsRequired();
        job.Property(x => x.LocalPrinterName).HasMaxLength(260);
        job.Property(x => x.IpAddress).HasMaxLength(64);
        job.Property(x => x.PayloadJson).IsRequired();
        job.Property(x => x.Status).HasMaxLength(20).IsRequired();
        job.Property(x => x.LastError).HasMaxLength(2000);
        job.Property(x => x.PendingRemoteStatus).HasMaxLength(20);
        job.Property(x => x.PendingRemoteError).HasMaxLength(2000);
        job.HasIndex(x => x.PrintJobId).IsUnique();
        job.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.CreatedAtUtc });
    }
}
