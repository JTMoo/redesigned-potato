namespace ReceiptService.Domain;

public sealed class ReceiptItem
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public Receipt Receipt { get; set; } = null!;
}
