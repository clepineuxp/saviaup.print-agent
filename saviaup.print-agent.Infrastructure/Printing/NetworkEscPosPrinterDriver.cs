using System.Net.Sockets;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Ports;
using SaviaUp.PrintAgent.Shared.Results;

namespace SaviaUp.PrintAgent.Infrastructure.Printing;

public sealed class NetworkEscPosPrinterDriver : IPrinterDriver
{
    public bool CanHandle(string connectionType)
        => connectionType is "NETWORK" or "ESC_POS_NETWORK";

    public async Task<Result> PrintAsync(PrinterTarget printer, ReadOnlyMemory<byte> document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(printer.IpAddress) || !printer.Port.HasValue)
            return Result.Failure("PRINTER_CONFIGURATION_INVALID", "Network printer address and port are required.");
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(printer.IpAddress, printer.Port.Value, cancellationToken);
            await client.GetStream().WriteAsync(document, cancellationToken);
            await client.GetStream().FlushAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            return Result.Failure("NETWORK_PRINT_FAILED", ex.Message);
        }
    }
}
