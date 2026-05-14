using Microsoft.AspNetCore.Mvc;

namespace ReceiptService.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "healthy", service = "receipt-service" });
}
