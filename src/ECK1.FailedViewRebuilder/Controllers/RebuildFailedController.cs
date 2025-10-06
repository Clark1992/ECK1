using ECK1.FailedViewRebuilder.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.FailedViewRebuilder.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(typeof(DateTimeOffset), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
public class RebuildFailedController(SampleRebuildRequestService service) : ControllerBase
{

    [HttpPost("sample")]
    public async Task<IActionResult> RebuildSample([FromQuery] int? count, CancellationToken ct)
    {
        service.SendRebuildRequests(e => e.FailureOccurredAt, true, count, e => e.SampleId, ct);

        return Accepted();
    }
}