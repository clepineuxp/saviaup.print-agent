using System.Net.Http.Json;
using System.Text.Json;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Backend;

public sealed class BackendClient(HttpClient httpClient) : IBackendClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<RemotePrintJob>> GetPendingJobsAsync(CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<RemotePrintJob[]>("api/printing/agent/jobs/pending?limit=100", JsonOptions, cancellationToken) ?? [];

    public async Task ReportStatusAsync(Guid printJobId, PrintJobStatusUpdate update, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync($"api/printing/agent/jobs/{printJobId:D}/status", update, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task HeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/printing/agent/heartbeat", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SyncPrintersAsync(IReadOnlyCollection<DiscoveredPrinter> printers, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/printing/agent/printers/sync", new SyncDiscoveredPrintersRequest(printers), JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
