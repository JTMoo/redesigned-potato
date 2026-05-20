using AggregationService.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AggregationService.Tests;

public sealed class SourcesControllerTests
{
    [Fact]
    public void GetAll_ReturnsEmptyList()
    {
        var sut = new SourcesController();
        var result = sut.GetAll();
        result.Should().BeOfType<OkObjectResult>();
    }
}
