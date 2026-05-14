using Microsoft.AspNetCore.Mvc;

namespace UserService.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "healthy", service = "user-service" });
}
