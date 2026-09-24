using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Infrastructure.Backend;

namespace SaviaUp.PrintAgent.IntegrationTests;

public sealed class HttpAgentDiscoveryConnectionTests
{
    [Fact]
    public async Task Discovery_RegistersPollsAndAcknowledgesTheCredential()
    {
        var discoveryId = Guid.NewGuid();
        var handler = new DiscoveryHandler(discoveryId);
        var connection = new HttpAgentDiscoveryConnection(
            new SingleClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://api.saviaup.test/") }),
            NullLogger<HttpAgentDiscoveryConnection>.Instance);

        var result = await connection.WaitForPairingAsync(
            new DiscoverAgentRequest("device-1", "POS-01", "Windows 11", "1.0.0", "192.168.1.8"),
            CancellationToken.None);
        await connection.AcknowledgePairingAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("device-token", result.DeviceToken);
        Assert.Equal(
            [
                "/api/printing/agent/discovery",
                $"/api/printing/agent/discovery/{discoveryId:D}/poll",
                $"/api/printing/agent/discovery/{discoveryId:D}/acknowledge"
            ],
            handler.Paths);
        Assert.All(handler.RequestBodies, body => Assert.Contains("discoverySecret", body));
        Assert.DoesNotContain("device-token", handler.RequestBodies[0]);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DiscoveryHandler(Guid discoveryId) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var payload = Paths.Count switch
            {
                1 => (object)new
                {
                    DiscoveryId = discoveryId,
                    // The local timestamp is only informational. The backend renews the lease on polling,
                    // so the agent must not stop merely because the original timestamp elapsed.
                    ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                    PollIntervalSeconds = 2
                },
                2 => new
                {
                    Status = "AUTHORIZED",
                    Pairing = new
                    {
                        AgentId = Guid.NewGuid(),
                        LocationId = Guid.NewGuid(),
                        DeviceToken = "device-token",
                        TokenExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
                        HeartbeatIntervalSeconds = 20,
                        PrintingHubPath = "/hubs/printing"
                    }
                },
                _ => new { }
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
