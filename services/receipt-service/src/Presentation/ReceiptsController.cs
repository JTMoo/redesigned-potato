using Microsoft.AspNetCore.Mvc;
using ReceiptService.Application.UseCases;

namespace ReceiptService.Presentation;

[ApiController]
[Route("receipts")]
public sealed class ReceiptsController : ControllerBase
{
    private readonly UploadReceiptUseCase _uploadUseCase;
    private readonly GetReceiptsUseCase _getReceiptsUseCase;
    private readonly GetReceiptUseCase _getReceiptUseCase;

    public ReceiptsController(
        UploadReceiptUseCase uploadUseCase,
        GetReceiptsUseCase getReceiptsUseCase,
        GetReceiptUseCase getReceiptUseCase)
    {
        ArgumentNullException.ThrowIfNull(uploadUseCase);
        ArgumentNullException.ThrowIfNull(getReceiptsUseCase);
        ArgumentNullException.ThrowIfNull(getReceiptUseCase);
        _uploadUseCase = uploadUseCase;
        _getReceiptsUseCase = getReceiptsUseCase;
        _getReceiptUseCase = getReceiptUseCase;
    }

    private static readonly HashSet<string> AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    [HttpPost]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? storeName,
        CancellationToken cancellationToken)
    {
        var userId = ParseUserId();
        if (userId is null)
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest("A file is required.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest("Only image files are accepted.");

        if (file.Length > MaxFileSizeBytes)
            return BadRequest("File must be smaller than 10 MB.");

        await using var stream = file.OpenReadStream();
        var dto = await _uploadUseCase.ExecuteAsync(
            userId.Value,
            stream,
            file.FileName,
            file.ContentType,
            storeName,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = ParseUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _getReceiptsUseCase.ExecuteAsync(userId.Value, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = ParseUserId();
        if (userId is null)
            return Unauthorized();

        var receipt = await _getReceiptUseCase.ExecuteAsync(id, userId.Value, cancellationToken);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    private Guid? ParseUserId()
    {
        var header = Request.Headers["X-User-Id"].FirstOrDefault();
        return Guid.TryParse(header, out var id) ? id : null;
    }
}
