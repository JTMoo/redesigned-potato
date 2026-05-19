using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReceiptService.Application.UseCases;
using ReceiptService.Data;
using ReceiptService.Domain;
using ReceiptService.Infrastructure.Ocr;
using ReceiptService.Infrastructure.Storage;
using Utilities;

namespace ReceiptService.Tests;

public sealed class UploadReceiptUseCaseTests
{
    private static ReceiptDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReceiptDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReceiptDbContext(options);
    }

    private static UploadReceiptUseCase CreateUseCase(
        ReceiptDbContext db,
        IReceiptStorage storage,
        IOcrService ocrService,
        IPublishEndpoint publishEndpoint)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        return new UploadReceiptUseCase(
            db,
            storage,
            ocrService,
            publishEndpoint,
            clock.Object,
            NullLogger<UploadReceiptUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_PersistsReceiptWithItemsAndPublishesBothEvents()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        var storage = new Mock<IReceiptStorage>();
        storage
            .Setup(s => s.UploadAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("some/path/file.jpg");

        var ocrItems = new List<ReceiptService.Infrastructure.Ocr.ExtractedItem>
        {
            new("Milk 1L", 1, 1.29m),
            new("Bread", 2, 0.89m),
            new("Orange Juice", 1, 2.49m),
        };
        var ocrService = new Mock<IOcrService>();
        ocrService
            .Setup(o => o.ExtractItemsAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrItems);

        var publishEndpoint = new Mock<IPublishEndpoint>();

        var sut = CreateUseCase(db, storage.Object, ocrService.Object, publishEndpoint.Object);

        // Act
        var result = await sut.ExecuteAsync(
            userId,
            new MemoryStream(new byte[] { 1, 2, 3 }),
            "receipt.jpg",
            "image/jpeg",
            "Test Store");

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.Items.Should().HaveCount(3);
        result.Status.Should().Be(ReceiptStatus.Processed.ToString());
        result.TotalAmount.Should().Be(1 * 1.29m + 2 * 0.89m + 1 * 2.49m);

        // Both events published
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<ReceiptCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<ItemsExtractedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Persisted to DB
        var dbReceipt = await db.Receipts.Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        dbReceipt.Should().NotBeNull();
        dbReceipt!.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteAsync_OcrThrows_ReceiptStaysProcessingAndItemsEventNotPublished()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        var storage = new Mock<IReceiptStorage>();
        storage
            .Setup(s => s.UploadAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("some/path/file.jpg");

        var ocrService = new Mock<IOcrService>();
        ocrService
            .Setup(o => o.ExtractItemsAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OCR engine unavailable"));

        var publishEndpoint = new Mock<IPublishEndpoint>();

        var sut = CreateUseCase(db, storage.Object, ocrService.Object, publishEndpoint.Object);

        // Act
        var result = await sut.ExecuteAsync(
            userId,
            new MemoryStream(new byte[] { 1, 2, 3 }),
            "receipt.jpg",
            "image/jpeg",
            null);

        // Assert — receipt persisted but still in Processing (OCR failed)
        result.Status.Should().Be(ReceiptStatus.Processing.ToString());

        // ReceiptCreatedEvent must still have been published
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<ReceiptCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // ItemsExtractedEvent must NOT be published
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<ItemsExtractedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
