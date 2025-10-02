using Xunit;
using Microsoft.AspNetCore.Mvc;
using ApiGateway.Controllers;
using FluentAssertions;

namespace ApiGateway.Tests.Controllers
{
    public class HomeControllerTests
    {
        private readonly HomeController _controller;

        public HomeControllerTests()
        {
            _controller = new HomeController();
        }

        [Fact]
        public void Index_ReturnsViewResult()
        {
            // Act
            var result = _controller.Index();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void Index_ReturnsDefaultView()
        {
            // Act
            var result = _controller.Index() as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result.ViewName.Should().BeNull(); // When ViewName is null, it uses the default view
        }
    }
}
