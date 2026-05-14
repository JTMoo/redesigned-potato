using FluentAssertions;
using MatchingService.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace MatchingService.Tests;

public sealed class MatchesControllerTests
{
    [Fact]
    public void GetAll_ReturnsEmptyList()
    {
        var sut = new MatchesController();

        var result = sut.GetAll();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(Array.Empty<object>());
    }
}
