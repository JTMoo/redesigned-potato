using Microsoft.AspNetCore.Mvc;

namespace MatchingService.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "healthy", service = "matching-service" });
}
