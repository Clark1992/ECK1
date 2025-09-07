using ECK1.CommandsAPI.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.CommandsAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(typeof(Success), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
public class SampleController : ControllerBase
{
    private readonly IMediator _mediator;

    public SampleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    //[HttpGet]
    //public async Task<ActionResult<IEnumerable<SampleModel>>> GetSamples()
    //{
    //    var samples = await _sampleService.GetAllSamplesAsync();
    //    return Ok(samples);
    //}

    //[HttpGet("{id}")]
    //public async Task<ActionResult<SampleModel>> GetSample(int id)
    //{
    //    _logger.LogInformation($"Fetching sample with id {id}");
    //    var sample = await _sampleService.GetSampleByIdAsync(id);
    //    if (sample == null)
    //    {
    //        return NotFound();
    //    }
    //    return Ok(sample);
    //}

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSampleCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToResult(result);
    }

    [HttpPut("{id}/name")]
    public async Task<IActionResult> ChangeName(Guid id, [FromBody] string newName)
    {
        var result = await _mediator.Send(new ChangeSampleNameCommand(id, newName));
        return this.ToResult(result);
    }

    [HttpPut("{id}/description")]
    public async Task<IActionResult> ChangeDescription(Guid id, [FromBody] string newDescription)
    {
        var result = await _mediator.Send(new ChangeSampleDescriptionCommand(id, newDescription));
        return this.ToResult(result);
    }
}