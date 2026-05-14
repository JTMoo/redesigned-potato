using FluentAssertions;
using ReceiptService.Features.Ocr;

namespace ReceiptService.Tests;

public sealed class TesseractOcrServiceTests
{
    [Fact]
    public async Task ExtractAsync_ReturnsEmptyResult()
    {
        var sut = new TesseractOcrService();
        using var stream = new MemoryStream();

        var result = await sut.ExtractAsync(stream);

        result.Items.Should().BeEmpty();
        result.TotalAmount.Should().Be(0m);
    }
}
