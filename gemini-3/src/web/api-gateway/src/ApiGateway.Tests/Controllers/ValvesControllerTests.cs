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
    public class ValvesControllerTests
    {
        private readonly Mock<IValveRepository> _mockValveRepository;
        private readonly Mock<IDauRepository> _mockDauRepository;
        private readonly Mock<ILogger<ValvesController>> _mockLogger;
        private readonly ValvesController _controller;

        public ValvesControllerTests()
        {
            _mockValveRepository = new Mock<IValveRepository>();
            _mockDauRepository = new Mock<IDauRepository>();
            _mockLogger = new Mock<ILogger<ValvesController>>();
            _controller = new ValvesController(_mockValveRepository.Object, _mockDauRepository.Object, _mockLogger.Object);

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

        #region GetAllValves Tests

        [Fact]
        public async Task GetAllValves_ReturnsOkResult_WithListOfValves()
        {
            // Arrange
            var expectedValves = new List<Valve>
            {
                new Valve 
                { 
                    Id = 1, 
                    Name = "Valve 1", 
                    Location = "Building A",
                    IsActive = true,
                    Atv = new Dau { Id = 1, SerialNumber = "ATV001" },
                    Remote = new Dau { Id = 2, SerialNumber = "REM001" }
                },
                new Valve 
                { 
                    Id = 2, 
                    Name = "Valve 2", 
                    Location = "Building B",
                    IsActive = false
                }
            };
            _mockValveRepository.Setup(repo => repo.GetAllVavlesAsync())
                .ReturnsAsync(expectedValves);

            // Act
            var result = await _controller.GetAllValves();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedValves = okResult.Value.Should().BeAssignableTo<IEnumerable<Valve>>().Subject;
            returnedValves.Should().HaveCount(2);
            returnedValves.Should().BeEquivalentTo(expectedValves);
        }

        [Fact]
        public async Task GetAllValves_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            _mockValveRepository.Setup(repo => repo.GetAllVavlesAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllValves();

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            statusCodeResult.Value.Should().Be("Internal server error");
        }

        #endregion

        #region GetAllValveConfigurations Tests

        [Fact]
        public async Task GetAllValveConfigurations_ReturnsOkResult_WithConfigurations()
        {
            // Arrange
            var expectedConfigs = new List<ValveConfiguration>
            {
                new ValveConfiguration 
                { 
                    Id = 1, 
                    ValveId = 1, 
                    ConfigurationType = "Pressure", 
                    ConfigurationValue = "100PSI" 
                },
                new ValveConfiguration 
                { 
                    Id = 2, 
                    ValveId = 2, 
                    ConfigurationType = "Temperature", 
                    ConfigurationValue = "75F" 
                }
            };
            _mockValveRepository.Setup(repo => repo.GetAllConfigurationsAsync())
                .ReturnsAsync(expectedConfigs);

            // Act
            var result = await _controller.GetAllValveConfigurations();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedConfigs = okResult.Value.Should().BeAssignableTo<IEnumerable<ValveConfiguration>>().Subject;
            returnedConfigs.Should().HaveCount(2);
            returnedConfigs.Should().BeEquivalentTo(expectedConfigs);
        }

        #endregion

        #region GetValveLogs Tests

        [Fact]
        public async Task GetValveLogs_ReturnsOkResult_WithLogs()
        {
            // Arrange
            var expectedLogs = new List<ValveLog>
            {
                new ValveLog 
                { 
                    Id = 1, 
                    ValveId = 1, 
                    LogType = LogType.Info, 
                    Message = "Valve opened",
                    TimeStamp = DateTime.UtcNow 
                },
                new ValveLog 
                { 
                    Id = 2, 
                    ValveId = 1, 
                    LogType = LogType.Warning, 
                    Message = "High pressure detected",
                    TimeStamp = DateTime.UtcNow.AddHours(-1) 
                }
            };
            _mockValveRepository.Setup(repo => repo.GetAllLogsAsync())
                .ReturnsAsync(expectedLogs);

            // Act
            var result = await _controller.GetValveLogs();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedLogs = okResult.Value.Should().BeAssignableTo<IEnumerable<ValveLog>>().Subject;
            returnedLogs.Should().HaveCount(2);
        }

        #endregion

        #region GetValveById Tests

        [Fact]
        public async Task GetValveById_ReturnsOkResult_WhenValveExists()
        {
            // Arrange
            var valveId = 1;
            var expectedValve = new Valve 
            { 
                Id = valveId, 
                Name = "Test Valve", 
                Location = "Building A",
                IsActive = true,
                Configurations = new List<ValveConfiguration>(),
                Logs = new List<ValveLog>()
            };
            _mockValveRepository.Setup(repo => repo.GetValveByIdAsync(valveId))
                .ReturnsAsync(expectedValve);

            // Act
            var result = await _controller.GetValveById(valveId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedValve = okResult.Value.Should().BeOfType<Valve>().Subject;
            returnedValve.Should().BeEquivalentTo(expectedValve);
        }

        [Fact]
        public async Task GetValveById_ReturnsNotFound_WhenValveDoesNotExist()
        {
            // Arrange
            var valveId = 999;
            _mockValveRepository.Setup(repo => repo.GetValveByIdAsync(valveId))
                .ReturnsAsync((Valve)null);

            // Act
            var result = await _controller.GetValveById(valveId);

            // Assert
            result.Result.Should().BeOfType<NotFoundResult>();
        }

        #endregion

        #region CreateValve Tests

        [Fact]
        public async Task CreateValve_ReturnsCreatedAtAction_WhenValveIsValid()
        {
            // Arrange
            var atvDau = new Dau { Id = 1, SerialNumber = "ATV001" };
            var remoteDau = new Dau { Id = 2, SerialNumber = "REM001" };
            
            var newValve = new Valve 
            { 
                Name = "New Valve",
                Location = "Building C",
                AtvId = 1,
                RemoteId = 2
            };

            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(1))
                .ReturnsAsync(atvDau);
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(2))
                .ReturnsAsync(remoteDau);
            
            _mockValveRepository.Setup(repo => repo.AddValveAsync(It.IsAny<Valve>()))
                .ReturnsAsync((Valve v) => 
                {
                    v.Id = 3;
                    return v;
                });
            
            _mockDauRepository.Setup(repo => repo.SetDauValveAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CreateValve(newValve);

            // Assert
            var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(ValvesController.GetValveById));
            createdResult.RouteValues["id"].Should().Be(3);
            
            // Verify DAU associations were set
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(3, 1), Times.Once);
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(3, 2), Times.Once);
        }

        [Fact]
        public async Task CreateValve_ReturnsBadRequest_WhenValveIsNull()
        {
            // Act
            var result = await _controller.CreateValve(null);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Valve data is null");
        }

        [Fact]
        public async Task CreateValve_ReturnsBadRequest_WhenAtvAndRemoteAreMissing()
        {
            // Arrange
            var newValve = new Valve 
            { 
                Name = "New Valve",
                Location = "Building C",
                AtvId = 0,
                RemoteId = 0
            };

            // Act
            var result = await _controller.CreateValve(newValve);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Valve must have both ATV and Remote DAUs");
        }

        #endregion

        #region UpdateValve Tests

        [Fact]
        public async Task UpdateValve_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var valveId = 1;
            var existingValve = new Valve 
            { 
                Id = valveId,
                Name = "Old Name",
                AtvId = 1,
                RemoteId = 2,
                Atv = new Dau { Id = 1 },
                Remote = new Dau { Id = 2 }
            };
            
            var updatedValve = new Valve 
            { 
                Id = valveId,
                Name = "Updated Name",
                Location = "Updated Location",
                AtvId = 3,
                RemoteId = 4
            };

            _mockValveRepository.Setup(repo => repo.GetValveByIdAsync(valveId))
                .ReturnsAsync(existingValve);
            
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(3))
                .ReturnsAsync(new Dau { Id = 3 });
            _mockDauRepository.Setup(repo => repo.GetDauByIdAsync(4))
                .ReturnsAsync(new Dau { Id = 4 });
            
            _mockDauRepository.Setup(repo => repo.SetDauValveAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            
            _mockValveRepository.Setup(repo => repo.UpdateValveAsync(It.IsAny<Valve>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateValveConfig(valveId, updatedValve);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            
            // Verify old DAUs were unassigned
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(0, 1), Times.Once);
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(0, 2), Times.Once);
            
            // Verify new DAUs were assigned
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(valveId, 3), Times.Once);
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(valveId, 4), Times.Once);
        }

        [Fact]
        public async Task UpdateValve_ReturnsNotFound_WhenValveDoesNotExist()
        {
            // Arrange
            var valveId = 999;
            var updatedValve = new Valve { Id = valveId };
            
            _mockValveRepository.Setup(repo => repo.GetValveByIdAsync(valveId))
                .ReturnsAsync((Valve)null);

            // Act
            var result = await _controller.UpdateValveConfig(valveId, updatedValve);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().Be($"Valve with id {valveId} not found");
        }

        [Fact]
        public async Task UpdateValve_ReturnsBadRequest_WhenIdMismatch()
        {
            // Arrange
            var valveId = 1;
            var updatedValve = new Valve { Id = 2 };

            // Act
            var result = await _controller.UpdateValveConfig(valveId, updatedValve);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("ID mismatch between URL and body");
        }

        #endregion

        #region DeleteValve Tests

        [Fact]
        public async Task DeleteValve_ReturnsNoContent_WhenValveExists()
        {
            // Arrange
            var valveId = 1;
            var existingValve = new Valve 
            { 
                Id = valveId,
                AtvId = 1,
                RemoteId = 2
            };
            
            _mockValveRepository.Setup(repo => repo.GetValveByIdAsync(valveId))
                .ReturnsAsync(existingValve);
            
            _mockDauRepository.Setup(repo => repo.SetDauValveAsync(0, It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            
            _mockValveRepository.Setup(repo => repo.DeleteValveAsync(valveId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteValve(valveId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            
            // Verify DAUs were detached
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(0, 1), Times.Once);
            _mockDauRepository.Verify(repo => repo.SetDauValveAsync(0, 2), Times.Once);
            
            // Verify valve was deleted
            _mockValveRepository.Verify(repo => repo.DeleteValveAsync(valveId), Times.Once);
        }

        [Fact]
        public async Task DeleteValve_ReturnsNotFound_WhenValveDoesNotExist()
        {
            // Arrange
            var valveId = 999;
            _mockValveRepository.Setup(repo => repo.GetValveByIdAsync(valveId))
                .ReturnsAsync((Valve)null);

            // Act
            var result = await _controller.DeleteValve(valveId);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
            _mockValveRepository.Verify(repo => repo.DeleteValveAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteValve_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var valveId = 1;
            _mockValveRepository.Setup(repo => repo.GetValveByIdAsync(valveId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.DeleteValve(valveId);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion
    }
}
