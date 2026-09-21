using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Device;

public sealed class WindowsDeviceIdentity : IDeviceIdentity
{
    public string DeviceIdentifier { get; } = CreateDeviceIdentifier();
    public string Hostname { get; } = Environment.MachineName;
    public string OperatingSystem { get; } = Environment.OSVersion.VersionString;
    public string Version { get; } = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
    public string? LocalIpAddress { get; } = ResolveLocalIp();

    private static string CreateDeviceIdentifier()
    {
        var machineGuid = System.OperatingSystem.IsWindows()
            ? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null)?.ToString()
            : null;
        var source = $"{Environment.MachineName}|{machineGuid ?? Environment.OSVersion.VersionString}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static string? ResolveLocalIp()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x))?.ToString();
        }
        catch (SocketException) { return null; }
    }
}
