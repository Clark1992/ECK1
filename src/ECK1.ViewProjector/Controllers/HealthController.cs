using Microsoft.AspNetCore.Mvc;

namespace ECK1.ViewProjector.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }
}
