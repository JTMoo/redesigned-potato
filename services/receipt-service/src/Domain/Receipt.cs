namespace ReceiptService.Domain;

public enum ReceiptStatus { Pending, Processing, Processed, Failed }

public sealed class Receipt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? ImagePath { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<ReceiptItem> Items { get; set; } = [];
}
