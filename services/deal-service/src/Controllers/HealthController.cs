using Microsoft.AspNetCore.Mvc;

namespace DealService.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "healthy", service = "deal-service" });
}
