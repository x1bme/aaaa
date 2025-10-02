using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ApiGateway.Controllers;
using ApiGateway.Models;
using ApiGateway.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ApiGateway.Tests.Controllers
{
    public class DauControllerTests
    {
        private readonly Mock<IDauRepository> _mockDauRepository;
        private readonly Mock<ILogger<DauController>> _mockLogger;
        private readonly DauController _controller;

        public DauControllerTests()
        {
            _mockDauRepository = new Mock<IDauRepository>();
            _mockLogger = new Mock<ILogger<DauController>>();
            _controller = new DauController(_mockDauRepository.Object, _mockLogger.Object);

            // Setup default user context for authorization
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.NameIdentifier, "123"),
            }, "mock"));

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };
        }

        #region GetAllDaus Tests

        [Fact]
        public async Task GetAllDaus_ReturnsOkResult_WithListOfDaus()
        {
            // Arrange
            var expectedDaus = new List<Dau>
            {
                new Dau { Id = 1, SerialNumber = "DAU001", Location = "Building A" },
                new Dau { Id = 2, SerialNumber = "DAU002", Location = "Building B" }
            };
            _mockDauRepository.Setup(repo => repo.GetAllDausAsync())
                .ReturnsAsync(expectedDaus);

            // Act
            var result = await _controller.GetAllDaus();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedDaus = okResult.Value.Should().BeAssignableTo<IEnumerable<Dau>>().Subject;
            returnedDaus.Should().HaveCount(2);
            returnedDaus.Should().BeEquivalentTo(expectedDaus);
        }

        [Fact]
        public async Task GetAllDaus_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            _mockDauRepository.Setup(repo => repo.GetAllDausAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllDaus();

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            statusCodeResult.Value.Should().Be("Internal server error");
        }

        #endregion

        #region GetDauById Tests

        [Fact]
        public async Task GetDauById_ReturnsOkResult_WhenDauExists()
        {
            // Arrange
            var dauId = 1;
            var expectedDau = new Dau 
            { 
                Id = dauId, 
                SerialNumber = "DAU001", 
                Location = "Building A",
                DauIPAddress = "192.168.1.100"
            };
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(dauId))
                .ReturnsAsync(expectedDau);

            // Act
            var result = await _controller.GetDauById(dauId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedDau = okResult.Value.Should().BeOfType<Dau>().Subject;
            returnedDau.Should().BeEquivalentTo(expectedDau);
        }

        [Fact]
        public async Task GetDauById_ReturnsNotFound_WhenDauDoesNotExist()
        {
            // Arrange
            var dauId = 999;
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(dauId))
                .ReturnsAsync((Dau)null);

            // Act
            var result = await _controller.GetDauById(dauId);

            // Assert
            result.Result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task GetDauById_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var dauId = 1;
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(dauId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetDauById(dauId);

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region CreateDau Tests

        [Fact]
        public async Task CreateDau_ReturnsCreatedAtAction_WhenDauIsValid()
        {
            // Arrange
            var newDau = new Dau 
            { 
                SerialNumber = "DAU003", 
                Location = "Building C",
                DauIPAddress = "192.168.1.102"
            };
            _mockDauRepository.Setup(repo => repo.AddDauAsync(It.IsAny<Dau>()))
                .ReturnsAsync((Dau d) => 
                {
                    d.Id = 3;
                    return d;
                });

            // Act
            var result = await _controller.CreateDau(newDau);

            // Assert
            var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(DauController.GetDauById));
            createdResult.RouteValues["id"].Should().Be(3);
            var returnedDau = createdResult.Value.Should().BeOfType<Dau>().Subject;
            returnedDau.Id.Should().Be(3);
        }

        [Fact]
        public async Task CreateDau_ReturnsBadRequest_WhenDauIsNull()
        {
            // Act
            var result = await _controller.CreateDau(null);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Dau data is null");
        }

        [Fact]
        public async Task CreateDau_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var newDau = new Dau { SerialNumber = "DAU003" };
            _mockDauRepository.Setup(repo => repo.AddDauAsync(It.IsAny<Dau>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.CreateDau(newDau);

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region UpdateDau Tests

        [Fact]
        public async Task UpdateDau_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var dauId = 1;
            var updatedDau = new Dau 
            { 
                Id = dauId,
                SerialNumber = "DAU001-Updated",
                Location = "Building A - Updated",
                DauIPAddress = "192.168.1.100"
            };
            _mockDauRepository.Setup(repo => repo.UpdateDau(It.IsAny<Dau>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateDau(dauId, updatedDau);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateDau_ReturnsBadRequest_WhenDauIsNull()
        {
            // Act
            var result = await _controller.UpdateDau(1, null);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("DAU data is null");
        }

        [Fact]
        public async Task UpdateDau_ReturnsBadRequest_WhenIdMismatch()
        {
            // Arrange
            var dauId = 1;
            var updatedDau = new Dau { Id = 2 }; // Different ID

            // Act
            var result = await _controller.UpdateDau(dauId, updatedDau);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("DAU ID mismatch between URL and body");
        }

        [Fact]
        public async Task UpdateDau_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var dauId = 1;
            var updatedDau = new Dau { Id = dauId };
            _mockDauRepository.Setup(repo => repo.UpdateDau(It.IsAny<Dau>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.UpdateDau(dauId, updatedDau);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region DeleteDau Tests

        [Fact]
        public async Task DeleteDau_ReturnsNoContent_WhenDauExists()
        {
            // Arrange
            var dauId = 1;
            var existingDau = new Dau { Id = dauId };
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(dauId))
                .ReturnsAsync(existingDau);
            _mockDauRepository.Setup(repo => repo.DeleteDauAsync(dauId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteValve(dauId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            _mockDauRepository.Verify(repo => repo.DeleteDauAsync(dauId), Times.Once);
        }

        [Fact]
        public async Task DeleteDau_ReturnsNotFound_WhenDauDoesNotExist()
        {
            // Arrange
            var dauId = 999;
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(dauId))
                .ReturnsAsync((Dau)null);

            // Act
            var result = await _controller.DeleteValve(dauId);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
            _mockDauRepository.Verify(repo => repo.DeleteDauAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteDau_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var dauId = 1;
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(dauId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.DeleteValve(dauId);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion
    }
}
