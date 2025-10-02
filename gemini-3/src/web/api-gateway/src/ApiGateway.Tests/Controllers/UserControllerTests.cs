using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using ApiGateway.Controllers;
using ApiGateway.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Linq;

namespace ApiGateway.Tests.Controllers
{
    public class UserControllerTests
    {
        private readonly Mock<UserManager<User>> _mockUserManager;
        private readonly UserController _controller;

        public UserControllerTests()
        {
            // Setup UserManager mock
            var userStore = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            _controller = new UserController(_mockUserManager.Object);

            // Setup admin user context for authorization
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };
        }

        #region GetUserById Tests

        [Fact]
        public async Task GetUserById_ReturnsOkResult_WithUserId()
        {
            // Arrange
            var userId = 1;

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be($"found user {userId}");
        }

        #endregion

        #region CreateUser Tests

        [Fact]
        public async Task CreateUser_ReturnsCreatedAtAction_WhenUserIsValid()
        {
            // Arrange
            var userModel = new GeminiUserModel
            {
                UserName = "newuser",
                Email = "newuser@test.com",
                Password = "Test123!",
                Role = "User"
            };

            var createdUser = new User
            {
                Id = "generated-id",
                UserName = userModel.UserName,
                Email = userModel.Email,
                Role = userModel.Role
            };

            _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<User>(), userModel.Password))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<User, string>((user, password) => 
                {
                    user.Id = createdUser.Id; // Simulate ID assignment
                });

            // Act
            var result = await _controller.CreateUser(userModel);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(UserController.GetUserById));
            createdResult.RouteValues["id"].Should().Be(createdUser.Id);
            
            var returnedUser = createdResult.Value.Should().BeOfType<User>().Subject;
            returnedUser.UserName.Should().Be(userModel.UserName);
            returnedUser.Email.Should().Be(userModel.Email);
            returnedUser.Role.Should().Be(userModel.Role);
        }

        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var userModel = new GeminiUserModel
            {
                UserName = "newuser",
                Email = "newuser@test.com",
                Password = "Test123!",
                Role = "User"
            };
            _controller.ModelState.AddModelError("UserName", "Username is required");

            // Act
            var result = await _controller.CreateUser(userModel);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenCreateFails()
        {
            // Arrange
            var userModel = new GeminiUserModel
            {
                UserName = "existinguser",
                Email = "existing@test.com",
                Password = "Test123!",
                Role = "User"
            };

            var errors = new List<IdentityError>
            {
                new IdentityError { Code = "DuplicateUserName", Description = "Username 'existinguser' is already taken." },
                new IdentityError { Code = "DuplicateEmail", Description = "Email 'existing@test.com' is already taken." }
            };

            _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<User>(), userModel.Password))
                .ReturnsAsync(IdentityResult.Failed(errors.ToArray()));

            // Act
            var result = await _controller.CreateUser(userModel);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            _controller.ModelState.ErrorCount.Should().Be(2);
            _controller.ModelState.Values
                .SelectMany(v => v.Errors)
                .Any(e => e.ErrorMessage.Contains("already taken"))
                .Should().BeTrue();
        }

        [Fact]
        public async Task CreateUser_HandlesPasswordComplexityError()
        {
            // Arrange
            var userModel = new GeminiUserModel
            {
                UserName = "newuser",
                Email = "newuser@test.com",
                Password = "weak",
                Role = "User"
            };

            var errors = new List<IdentityError>
            {
                new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be at least 6 characters." },
                new IdentityError { Code = "PasswordRequiresUpper", Description = "Passwords must have at least one uppercase ('A'-'Z')." }
            };

            _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<User>(), userModel.Password))
                .ReturnsAsync(IdentityResult.Failed(errors.ToArray()));

            // Act
            var result = await _controller.CreateUser(userModel);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            _controller.ModelState.ErrorCount.Should().Be(2);
        }

        #endregion

        #region UpdateValveConfig Tests

        [Fact]
        public async Task UpdateValveConfig_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var userId = 1;
            var updatedUser = new GeminiUserModel
            {
                UserName = "updateduser",
                Email = "updated@test.com",
                Role = "Admin"
            };

            // Act
            var result = await _controller.UpdateValveConfig(userId, updatedUser);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        #endregion

        #region DeleteUser Tests

        [Fact]
        public async Task DeleteUser_ReturnsNoContent_WhenDeletionIsSuccessful()
        {
            // Arrange
            var userId = 1;

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        #endregion
    }
}
