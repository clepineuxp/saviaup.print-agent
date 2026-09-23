namespace SaviaUp.PrintAgent.Domain.Options;

public sealed class AgentOptions
{
    public const string SectionName = "PrintAgent";
    public string BackendUrl { get; set; } = "http://localhost:5000";
    public string DataDirectory { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 30;
    public int HeartbeatIntervalSeconds { get; set; } = 20;
    public int MaximumAttempts { get; set; } = 10;
    public int InitialRetryDelaySeconds { get; set; } = 5;
    public int MaximumRetryDelaySeconds { get; set; } = 300;
}
