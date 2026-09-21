using System.ComponentModel;
using System.Runtime.InteropServices;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Printing;

public sealed class WindowsPrinterDiscovery : IInstalledPrinterDiscovery
{
    private const uint PrinterEnumLocal = 2;
    private const uint PrinterEnumConnections = 4;
    private const uint PrinterAttributeDefault = 0x4;

    public Task<IReadOnlyCollection<DiscoveredPrinter>> GetInstalledAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult<IReadOnlyCollection<DiscoveredPrinter>>([]);
        return Task.Run<IReadOnlyCollection<DiscoveredPrinter>>(Discover, cancellationToken);
    }

    private static IReadOnlyCollection<DiscoveredPrinter> Discover()
    {
        EnumPrinters(PrinterEnumLocal | PrinterEnumConnections, null, 4, IntPtr.Zero, 0, out var required, out _);
        if (required == 0) return [];
        var buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            if (!EnumPrinters(PrinterEnumLocal | PrinterEnumConnections, null, 4, buffer, required, out _, out var returned))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var size = Marshal.SizeOf<PrinterInfo4>();
            var result = new List<DiscoveredPrinter>((int)returned);
            for (var index = 0; index < returned; index++)
            {
                var info = Marshal.PtrToStructure<PrinterInfo4>(IntPtr.Add(buffer, index * size));
                var name = Marshal.PtrToStringUni(info.PrinterName);
                if (!string.IsNullOrWhiteSpace(name)) result.Add(new DiscoveredPrinter(name, (info.Attributes & PrinterAttributeDefault) != 0));
            }
            return result;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo4
    {
        public IntPtr PrinterName;
        public IntPtr ServerName;
        public uint Attributes;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr buffer, uint size, out uint needed, out uint returned);
}
