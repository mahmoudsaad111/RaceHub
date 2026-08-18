using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RaceHub.Infrastructure.Messaging;

namespace RaceHub.API.Controllers;

/// <summary>
/// Messaging diagnostics: queue depths straight from the RabbitMQ
/// management HTTP API. This is the observability leg of the reliability
/// story — the retry/DLQ topology only means something if you can SEE a
/// .dlq growing when a consumer is failing, so this surfaces
/// messages-ready per queue (including the .retry/.dlq ones) without
/// having to open the management UI on :15672.
/// </summary>
[Route("api/messaging")]
[Authorize]
public class MessagingController : ApiControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RabbitMqOptions _rabbitOptions;

    public MessagingController(IHttpClientFactory httpClientFactory, IOptions<RabbitMqOptions> rabbitOptions)
    {
        _httpClientFactory = httpClientFactory;
        _rabbitOptions = rabbitOptions.Value;
    }

    [HttpGet("queue-depths")]
    public async Task<IActionResult> GetQueueDepths(CancellationToken cancellationToken)
    {
        var baseUrl = _rabbitOptions.ManagementUrl ?? $"http://{_rabbitOptions.HostName}:15672";

        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/api/queues");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_rabbitOptions.UserName}:{_rabbitOptions.Password}")));

        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = $"RabbitMQ management API returned {(int)response.StatusCode}.",
                errorCode = "management_api_unavailable"
            });
        }

        // Only the vhost-relevant fields from
        // https://rabbitmq.com/management.html#http-api — the full queue
        // objects are enormous; don't forward them all to the browser.
        var queues = await response.Content.ReadFromJsonAsync<List<QueueDepthDto>>(cancellationToken);

        var result = (queues ?? [])
            .OrderBy(q => q.Name, StringComparer.Ordinal)
            .ToList();

        return Ok(new { success = true, message = "Queue depths retrieved.", data = result });
    }

    // Only the vhost-relevant fields from
    // https://rabbitmq.com/management.html#http-api — the full queue
    // objects are enormous; don't forward them all to the browser. Keys
    // are snake_case in the management API, hence the explicit
    // JsonPropertyName mappings.
    private sealed record QueueDepthDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("messages")] int Messages,
        [property: JsonPropertyName("messages_ready")] int MessagesReady,
        [property: JsonPropertyName("messages_unacknowledged")] int MessagesUnacknowledged);
}
