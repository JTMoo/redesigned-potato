using ApiGateway.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.Tests;

public sealed class JwtServiceTests
{
    private static JwtService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "test-secret-key-minimum-32-characters-long"
            })
            .Build();
        return new JwtService(config);
    }

    [Fact]
    public void Issue_ThenExtract_ReturnsOriginalUserId()
    {
        var sut = CreateService();
        const string userId = "user-123";

        var token = sut.Issue(userId, "user@example.com", "Test User");
        var extractedId = sut.TryExtractUserId(token);

        extractedId.Should().Be(userId);
    }

    [Fact]
    public void TryExtractUserId_WithInvalidToken_ReturnsNull()
    {
        var sut = CreateService();

        var result = sut.TryExtractUserId("not-a-valid-token");

        result.Should().BeNull();
    }
}
