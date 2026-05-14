using Microsoft.AspNetCore.Mvc;

namespace MatchingService.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class MatchesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(Array.Empty<object>());
}
