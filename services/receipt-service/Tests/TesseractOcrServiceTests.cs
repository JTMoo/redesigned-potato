using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReceiptService.Infrastructure.Ocr;

namespace ReceiptService.Tests;

public sealed class TesseractOcrServiceTests
{
    [Fact]
    public async Task ExtractItemsAsync_ReturnsThreeHardcodedItems()
    {
        var sut = new TesseractOcrService(NullLogger<TesseractOcrService>.Instance);
        using var stream = new MemoryStream();

        var result = await sut.ExtractItemsAsync(stream);

        result.Should().HaveCount(3);
        result[0].Description.Should().Be("Milk 1L");
        result[0].UnitPrice.Should().Be(1.29m);
        result[1].Description.Should().Be("Bread");
        result[1].Quantity.Should().Be(2);
        result[2].Description.Should().Be("Orange Juice");
        result[2].UnitPrice.Should().Be(2.49m);
    }
}
