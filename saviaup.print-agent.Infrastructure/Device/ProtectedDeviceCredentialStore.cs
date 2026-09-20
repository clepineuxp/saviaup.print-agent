using System.Security.Cryptography;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Device;

public sealed class ProtectedDeviceCredentialStore(AgentPathResolver paths) : IDeviceCredentialStore
{
    private static readonly byte[] Entropy = "SaviaUp.PrintAgent.DeviceToken.v1"u8.ToArray();

    public async Task<string?> ReadTokenAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The Savia Up Print Agent credential store requires Windows DPAPI.");
        if (!File.Exists(paths.CredentialPath)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(paths.CredentialPath, cancellationToken);
        var clear = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
        return System.Text.Encoding.UTF8.GetString(clear);
    }

    public async Task WriteTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The Savia Up Print Agent credential store requires Windows DPAPI.");
        var clear = System.Text.Encoding.UTF8.GetBytes(token);
        var protectedBytes = ProtectedData.Protect(clear, Entropy, DataProtectionScope.LocalMachine);
        await File.WriteAllBytesAsync(paths.CredentialPath, protectedBytes, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(paths.CredentialPath)) File.Delete(paths.CredentialPath);
        return Task.CompletedTask;
    }
}
