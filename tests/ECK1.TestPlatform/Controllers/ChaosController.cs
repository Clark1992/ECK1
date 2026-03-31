using ECK1.CommonUtils.Chaos;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.TestPlatform.Controllers;

/// <summary>
/// Controls chaos/failure simulation across platform services.
/// Activate scenarios on target services to test resilience mechanisms.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ChaosController(
    IHttpClientFactory httpClientFactory,
    ILogger<ChaosController> logger) : ControllerBase
{
    /// <summary>
    /// Lists all available chaos scenarios with descriptions.
    /// </summary>
    [HttpGet("scenarios")]
    public ActionResult<IReadOnlyList<ScenarioInfo>> GetScenarios() =>
        Ok(ChaosScenarios.All);

    /// <summary>
    /// Gets the current chaos status from a target service.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(
        [FromQuery] string serviceUrl,
        CancellationToken ct)
    {
        var client = CreateClient(serviceUrl);
        var response = await client.GetAsync("chaos/status", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return Content(body, "application/json");
    }

    /// <summary>
    /// Activates a chaos scenario on a target service.
    /// Example: POST /api/chaos/activate?serviceUrl=http://integration-proxy-elasticsearch-release&amp;scenarioId=proxy.drop-event
    /// </summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate(
        [FromQuery] string serviceUrl,
        [FromQuery] string scenarioId,
        CancellationToken ct)
    {
        logger.LogInformation("Activating chaos scenario '{ScenarioId}' on {ServiceUrl}", scenarioId, serviceUrl);

        var client = CreateClient(serviceUrl);
        var response = await client.PostAsync($"chaos/activate/{scenarioId}", null, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return Content(body, "application/json");
    }

    /// <summary>
    /// Deactivates a chaos scenario on a target service.
    /// </summary>
    [HttpDelete("activate")]
    public async Task<IActionResult> Deactivate(
        [FromQuery] string serviceUrl,
        [FromQuery] string scenarioId,
        CancellationToken ct)
    {
        logger.LogInformation("Deactivating chaos scenario '{ScenarioId}' on {ServiceUrl}", scenarioId, serviceUrl);

        var client = CreateClient(serviceUrl);
        var response = await client.DeleteAsync($"chaos/activate/{scenarioId}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return Content(body, "application/json");
    }

    /// <summary>
    /// Deactivates all chaos scenarios on a target service.
    /// </summary>
    [HttpDelete("deactivate-all")]
    public async Task<IActionResult> DeactivateAll(
        [FromQuery] string serviceUrl,
        CancellationToken ct)
    {
        logger.LogInformation("Deactivating all chaos scenarios on {ServiceUrl}", serviceUrl);

        var client = CreateClient(serviceUrl);
        var response = await client.DeleteAsync("chaos", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return Content(body, "application/json");
    }

    /// <summary>
    /// Activates a scenario on ALL configured target services at once.
    /// </summary>
    [HttpPost("activate-all")]
    public async Task<IActionResult> ActivateOnAll(
        [FromQuery] string scenarioId,
        [FromBody] string[] serviceUrls,
        CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var url in serviceUrls)
        {
            try
            {
                var client = CreateClient(url);
                var response = await client.PostAsync($"chaos/activate/{scenarioId}", null, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                results.Add(new { Service = url, Status = "ok", Response = body });
            }
            catch (Exception ex)
            {
                results.Add(new { Service = url, Status = "error", Error = ex.Message });
            }
        }
        return Ok(results);
    }

    private HttpClient CreateClient(string serviceUrl)
    {
        var client = httpClientFactory.CreateClient("chaos");
        client.BaseAddress = new Uri(serviceUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }
}
