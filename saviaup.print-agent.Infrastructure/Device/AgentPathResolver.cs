using Microsoft.Extensions.Options;
using SaviaUp.PrintAgent.Domain.Options;

namespace SaviaUp.PrintAgent.Infrastructure.Device;

public sealed class AgentPathResolver
{
    public AgentPathResolver(IOptions<AgentOptions> configuredOptions)
    {
        var configured = configuredOptions.Value.DataDirectory;
        DataDirectory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SaviaUp", "PrintAgent")
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "print-queue.db");
    public string CredentialPath => Path.Combine(DataDirectory, "device-token.dat");
    public string LogDirectory => Path.Combine(DataDirectory, "logs");
}
