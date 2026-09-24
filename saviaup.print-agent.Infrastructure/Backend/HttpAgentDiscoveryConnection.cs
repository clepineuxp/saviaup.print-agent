using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Ports;

namespace SaviaUp.PrintAgent.Infrastructure.Backend;

public sealed class HttpAgentDiscoveryConnection(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpAgentDiscoveryConnection> logger) : IAgentDiscoveryConnection
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _stateLock = new();
    private DiscoveryState? _pendingAcknowledgement;

    public async Task<PairAgentResponse?> WaitForPairingAsync(
        DiscoverAgentRequest request,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("PrintAgentDiscovery");
        while (!cancellationToken.IsCancellationRequested)
        {
            var secret = CreateSecret();
            using var registrationResponse = await client.PostAsJsonAsync(
                "api/printing/agent/discovery",
                new RegisterAgentDiscoveryRequest(
                    secret,
                    request.DeviceIdentifier,
                    request.Hostname,
                    request.OperatingSystem,
                    request.Version,
                    request.LocalIpAddress),
                JsonOptions,
                cancellationToken);
            registrationResponse.EnsureSuccessStatusCode();
            var registration = await registrationResponse.Content.ReadFromJsonAsync<RegisterAgentDiscoveryResponse>(
                JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("The backend returned an empty discovery registration.");

            logger.LogInformation(
                "Print agent is available for linking from Savia Up. Its discovery lease is renewed while this process remains connected.");
            var interval = TimeSpan.FromSeconds(Math.Clamp(registration.PollIntervalSeconds, 2, 15));
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);
                using var pollResponse = await client.PostAsJsonAsync(
                    $"api/printing/agent/discovery/{registration.DiscoveryId:D}/poll",
                    new PollAgentDiscoveryRequest(secret),
                    JsonOptions,
                    cancellationToken);
                if (pollResponse.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)pollResponse.StatusCode >= 500)
                {
                    logger.LogWarning(
                        "Print agent discovery polling received {StatusCode}; it will retry.",
                        (int)pollResponse.StatusCode);
                    continue;
                }

                if (pollResponse.StatusCode is HttpStatusCode.BadRequest
                    or HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.NotFound
                    or HttpStatusCode.Gone
                    or HttpStatusCode.UnprocessableEntity)
                {
                    logger.LogWarning(
                        "Print agent discovery registration is no longer valid ({StatusCode}); it will register again.",
                        (int)pollResponse.StatusCode);
                    break;
                }

                pollResponse.EnsureSuccessStatusCode();
                var poll = await pollResponse.Content.ReadFromJsonAsync<PollAgentDiscoveryResponse>(
                    JsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException("The backend returned an empty discovery response.");
                if (poll.Pairing is null) continue;

                lock (_stateLock)
                    _pendingAcknowledgement = new DiscoveryState(registration.DiscoveryId, secret);
                return poll.Pairing;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        return null;
    }

    public async Task AcknowledgePairingAsync(CancellationToken cancellationToken)
    {
        DiscoveryState? state;
        lock (_stateLock) state = _pendingAcknowledgement;
        if (state is null) return;

        var client = httpClientFactory.CreateClient("PrintAgentDiscovery");
        using var response = await client.PostAsJsonAsync(
            $"api/printing/agent/discovery/{state.DiscoveryId:D}/acknowledge",
            new PollAgentDiscoveryRequest(state.Secret),
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        lock (_stateLock)
        {
            if (_pendingAcknowledgement == state) _pendingAcknowledgement = null;
        }
    }

    private static string CreateSecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed record DiscoveryState(Guid DiscoveryId, string Secret);
}
