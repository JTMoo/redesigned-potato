namespace ReceiptService.Application.DTOs;

public sealed record ReceiptItemDto(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total
);

public sealed record ReceiptDto(
    Guid Id,
    Guid UserId,
    string StoreName,
    decimal TotalAmount,
    string? ImagePath,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReceiptItemDto> Items
);
