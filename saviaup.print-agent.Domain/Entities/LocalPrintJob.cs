namespace SaviaUp.PrintAgent.Domain.Entities;

public static class LocalPrintJobStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Printed = "PRINTED";
    public const string Failed = "FAILED";
}

public sealed class LocalPrintJob
{
    public Guid Id { get; set; }
    public Guid PrintJobId { get; set; }
    public Guid PrinterId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string? LocalPrinterName { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public int PaperWidth { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = LocalPrintJobStatuses.Pending;
    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? PendingRemoteStatus { get; set; }
    public string? PendingRemoteError { get; set; }
}
