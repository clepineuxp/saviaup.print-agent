using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Options;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Backend;

public sealed class SignalRAgentDiscoveryConnection(
    IOptions<AgentOptions> configuredOptions,
    ILogger<SignalRAgentDiscoveryConnection> logger) : IAgentDiscoveryConnection
{
    public async Task<PairAgentResponse?> WaitForPairingAsync(
        DiscoverAgentRequest request,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<PairAgentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var url = configuredOptions.Value.BackendUrl.TrimEnd('/') + "/hubs/printing-discovery";
        await using var connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .Build();
        connection.On<PairAgentResponse>("OnAgentPaired", response => completion.TrySetResult(response));
        connection.Reconnected += async _ =>
        {
            await connection.InvokeAsync("Register", request, CancellationToken.None);
            logger.LogInformation("Print agent discovery connection restored.");
        };
        connection.Reconnecting += _ =>
        {
            logger.LogWarning("Print agent discovery connection interrupted; waiting to reconnect.");
            return Task.CompletedTask;
        };

        await connection.StartAsync(cancellationToken);
        await connection.InvokeAsync("Register", request, cancellationToken);
        logger.LogInformation("Print agent is available for linking from Savia Up.");
        return await completion.Task.WaitAsync(cancellationToken);
    }
}
