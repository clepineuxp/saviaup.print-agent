using System.ComponentModel;
using System.Runtime.InteropServices;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Ports;
using SaviaUp.PrintAgent.Shared.Results;

namespace SaviaUp.PrintAgent.Infrastructure.Printing;

public sealed class WindowsSpoolerPrinterDriver : IPrinterDriver
{
    public bool CanHandle(string connectionType) => connectionType == "WINDOWS_SPOOLER";

    public Task<Result> PrintAsync(PrinterTarget printer, ReadOnlyMemory<byte> document, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult(Result.Failure("WINDOWS_REQUIRED", "Windows Print Spooler is only available on Windows."));
        if (string.IsNullOrWhiteSpace(printer.LocalPrinterName)) return Task.FromResult(Result.Failure("PRINTER_CONFIGURATION_INVALID", "Local printer name is required."));
        return Task.Run(() => PrintRaw(printer.LocalPrinterName, document.ToArray()), cancellationToken);
    }

    private static Result PrintRaw(string printerName, byte[] bytes)
    {
        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero)) return Failure("OPEN_PRINTER_FAILED");
        try
        {
            var doc = new DocInfo { DocumentName = "Savia Up Print Job", DataType = "RAW" };
            if (StartDocPrinter(handle, 1, ref doc) == 0) return Failure("START_DOCUMENT_FAILED");
            try
            {
                if (!StartPagePrinter(handle)) return Failure("START_PAGE_FAILED");
                try
                {
                    var pointer = Marshal.AllocCoTaskMem(bytes.Length);
                    try
                    {
                        Marshal.Copy(bytes, 0, pointer, bytes.Length);
                        if (!WritePrinter(handle, pointer, bytes.Length, out var written) || written != bytes.Length)
                            return Failure("WRITE_PRINTER_FAILED");
                    }
                    finally { Marshal.FreeCoTaskMem(pointer); }
                }
                finally { EndPagePrinter(handle); }
            }
            finally { EndDocPrinter(handle); }
            return Result.Success();
        }
        finally { ClosePrinter(handle); }
    }

    private static Result Failure(string code)
        => Result.Failure(code, new Win32Exception(Marshal.GetLastWin32Error()).Message);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool ClosePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)] private static extern int StartDocPrinter(IntPtr handle, int level, ref DocInfo docInfo);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool StartPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(IntPtr handle, IntPtr bytes, int count, out int written);
}
