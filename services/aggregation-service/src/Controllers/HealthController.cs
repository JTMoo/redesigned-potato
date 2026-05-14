using Microsoft.AspNetCore.Mvc;

namespace AggregationService.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "healthy", service = "aggregation-service" });
}
