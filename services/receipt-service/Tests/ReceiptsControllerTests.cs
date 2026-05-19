using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReceiptService.Application.DTOs;
using ReceiptService.Application.UseCases;
using ReceiptService.Data;
using ReceiptService.Domain;
using ReceiptService.Infrastructure.Ocr;
using ReceiptService.Infrastructure.Storage;
using ReceiptService.Presentation;
using Utilities;

namespace ReceiptService.Tests;

public sealed class ReceiptsControllerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ReceiptDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReceiptDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReceiptDbContext(options);
    }

    private static ReceiptsController CreateController(ReceiptDbContext db, Guid? userId = null)
    {
        var storageMock = new Mock<IReceiptStorage>();
        storageMock
            .Setup(s => s.UploadAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("user/receipt/file.jpg");

        var ocrMock = new Mock<IOcrService>();
        ocrMock
            .Setup(o => o.ExtractItemsAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var publishMock = new Mock<IPublishEndpoint>();
        var clockMock = new Mock<IDateTimeProvider>();
        clockMock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var uploadUseCase = new UploadReceiptUseCase(
            db,
            storageMock.Object,
            ocrMock.Object,
            publishMock.Object,
            clockMock.Object,
            NullLogger<UploadReceiptUseCase>.Instance);

        var getReceiptsUseCase = new GetReceiptsUseCase(
            db,
            NullLogger<GetReceiptsUseCase>.Instance);

        var getReceiptUseCase = new GetReceiptUseCase(
            db,
            NullLogger<GetReceiptUseCase>.Instance);

        var controller = new ReceiptsController(uploadUseCase, getReceiptsUseCase, getReceiptUseCase);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        if (userId.HasValue)
            controller.HttpContext.Request.Headers["X-User-Id"] = userId.Value.ToString();

        return controller;
    }

    private static Mock<IFormFile> CreateFileMock(
        string contentType, long length, string fileName = "receipt.jpg")
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[4]));
        return mock;
    }

    // ---------------------------------------------------------------------------
    // File-type validation
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    [InlineData("video/mp4")]
    public async Task Upload_DisallowedContentType_ReturnsBadRequestWithExpectedMessage(string contentType)
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var controller = CreateController(db, Guid.NewGuid());
        var file = CreateFileMock(contentType, 512);

        // Act
        var result = await controller.Upload(file.Object, null, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Only image files are accepted.");
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    public async Task Upload_AllowedContentType_DoesNotReturnContentTypeError(string contentType)
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var controller = CreateController(db, Guid.NewGuid());
        var file = CreateFileMock(contentType, 512, $"img.{contentType.Split('/')[1]}");

        // Act
        var result = await controller.Upload(file.Object, null, CancellationToken.None);

        // Assert — must not be a content-type rejection
        if (result is BadRequestObjectResult badRequest)
            badRequest.Value.Should().NotBe("Only image files are accepted.");
    }

    // ---------------------------------------------------------------------------
    // File-size validation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Upload_FileLargerThan10MB_ReturnsBadRequestWithExpectedMessage()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var controller = CreateController(db, Guid.NewGuid());
        var file = CreateFileMock("image/jpeg", 10 * 1024 * 1024 + 1, "big.jpg");

        // Act
        var result = await controller.Upload(file.Object, null, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("File must be smaller than 10 MB.");
    }

    [Fact]
    public async Task Upload_FileExactly10MB_IsNotRejectedForSize()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var controller = CreateController(db, Guid.NewGuid());
        var file = CreateFileMock("image/png", 10 * 1024 * 1024, "max.png");

        // Act
        var result = await controller.Upload(file.Object, null, CancellationToken.None);

        // Assert — must not be a size rejection
        if (result is BadRequestObjectResult badRequest)
            badRequest.Value.Should().NotBe("File must be smaller than 10 MB.");
    }

    // ---------------------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Upload_MissingUserIdHeader_ReturnsUnauthorized()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        // No userId provided — X-User-Id header will be absent
        var controller = CreateController(db, userId: null);
        var file = CreateFileMock("image/jpeg", 512);

        // Act
        var result = await controller.Upload(file.Object, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ---------------------------------------------------------------------------
    // Size check takes priority over type check (size checked after type)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Upload_WrongTypeAndTooLarge_ReturnsTypeErrorFirst()
    {
        // Arrange — content type is checked before size
        await using var db = CreateInMemoryDb();
        var controller = CreateController(db, Guid.NewGuid());
        var file = CreateFileMock("application/pdf", 20 * 1024 * 1024, "doc.pdf");

        // Act
        var result = await controller.Upload(file.Object, null, CancellationToken.None);

        // Assert — type error is returned (it is checked first in the controller)
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Only image files are accepted.");
    }
}
