using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Models;
using ECK1.FailedViewRebuilder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ECK1.FailedViewRebuilder.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class RebuildFailedController(
    IRebuildRequestService service) : ControllerBase
{
    [HttpGet("/api/failed")]
    [ProducesResponseType(typeof(FailedViewsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview([FromQuery] int? count) =>
        Ok(await service.GetFailedViewsOverview(count));
}