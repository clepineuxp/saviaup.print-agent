namespace SaviaUp.PrintAgent.Domain.Contracts;

public sealed record PairAgentResponse(
    Guid AgentId,
    Guid LocationId,
    string DeviceToken,
    DateTimeOffset TokenExpiresAt,
    int HeartbeatIntervalSeconds,
    string PrintingHubPath);

public sealed record DiscoverAgentRequest(
    string DeviceIdentifier,
    string Hostname,
    string OperatingSystem,
    string Version,
    string? LocalIpAddress);

public sealed record RemotePrintJob(
    Guid Id,
    Guid PrinterId,
    string PrinterName,
    string PrinterConnectionType,
    string? LocalPrinterName,
    string? PrinterIpAddress,
    int? PrinterPort,
    int PaperWidth,
    string PayloadJson,
    string Status,
    bool IsReprint);

public sealed record PrintJobStatusUpdate(string Status, string? Error);
public sealed record HeartbeatRequest(string Version, string? LocalIpAddress, string? Status);
public sealed record DiscoveredPrinter(string Name, bool IsDefault);
public sealed record SyncDiscoveredPrintersRequest(IReadOnlyCollection<DiscoveredPrinter> Printers);

public sealed record KitchenOrderPrintItem(
    int Quantity,
    string Name,
    IReadOnlyCollection<string> Modifiers,
    string? Notes);

public sealed record KitchenOrderPrintPayload(
    string DocumentType,
    string OrderNumber,
    string? Table,
    string Waiter,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<KitchenOrderPrintItem> Items,
    string? Notes,
    bool IsReprint,
    string? PrinterName = null,
    string? OrganizationName = null,
    string? FooterMessage = null);

public sealed record PrinterTarget(
    Guid PrinterId,
    string Name,
    string ConnectionType,
    string? LocalPrinterName,
    string? IpAddress,
    int? Port,
    int PaperWidth);
