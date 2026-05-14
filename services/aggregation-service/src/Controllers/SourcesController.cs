using Microsoft.AspNetCore.Mvc;

namespace AggregationService.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class SourcesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(Array.Empty<object>());
}
