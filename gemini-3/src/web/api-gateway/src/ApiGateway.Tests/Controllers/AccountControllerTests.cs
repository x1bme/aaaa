using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using ApiGateway.Controllers;
using ApiGateway.Models;
using FluentAssertions;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ApiGateway.Tests.Controllers
{
    public class AccountControllerTests
    {
        private readonly Mock<SignInManager<User>> _mockSignInManager;
        private readonly Mock<UserManager<User>> _mockUserManager;
        private readonly AccountController _controller;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IServiceProvider> _mockServiceProvider;

        public AccountControllerTests()
        {
            // Setup UserManager mock
            var userStore = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // Setup SignInManager mock
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
            _mockSignInManager = new Mock<SignInManager<User>>(
                _mockUserManager.Object,
                contextAccessor.Object,
                userPrincipalFactory.Object,
                null, null, null, null);

            // Setup authentication service
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IAuthenticationService)))
                .Returns(_mockAuthService.Object);

            // Create controller
            _controller = new AccountController(_mockSignInManager.Object, _mockUserManager.Object);

            // Setup controller context
            var httpContext = new DefaultHttpContext
            {
                RequestServices = _mockServiceProvider.Object
            };
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Setup TempData
            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempDataDictionaryFactory = new TempDataDictionaryFactory(tempDataProvider.Object);
            _controller.TempData = tempDataDictionaryFactory.GetTempData(httpContext);

            // Setup URL helper
            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            _controller.Url = urlHelper.Object;
        }

        #region Login GET Tests

        [Fact]
        public void Login_Get_ReturnsViewResult()
        {
            // Arrange
            var returnUrl = "/home/index";

            // Act
            var result = _controller.Login(returnUrl: returnUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewData["ReturnUrl"].Should().Be(returnUrl);
        }

        [Fact]
        public void Login_Get_ReturnsViewResult_WhenReturnUrlIsNull()
        {
            // Act
            var result = _controller.Login(returnUrl: null);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewData["ReturnUrl"].Should().BeNull();
        }

        #endregion

        #region Login POST Tests

        [Fact]
        public async Task Login_Post_ReturnsRedirectToReturnUrl_WhenCredentialsAreValid()
        {
            // Arrange
            var model = new LoginView
            {
                Username = "testuser",
                Password = "Test123!",
                ReturnUrl = "/home/index"
            };

            var user = new User
            {
                UserName = model.Username,
                Email = "test@test.com",
                Role = "User"
            };

            _mockUserManager.Setup(um => um.FindByNameAsync(model.Username))
                .ReturnsAsync(user);
            _mockUserManager.Setup(um => um.CheckPasswordAsync(user, model.Password))
                .ReturnsAsync(true);
            _mockSignInManager.Setup(sm => sm.SignInAsync(user, true, null))
                .Returns(Task.CompletedTask);

            _mockAuthService.Setup(auth => auth.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Login(model);

            // Assert
            result.Should().BeOfType<RedirectResult>();
            var redirectResult = result as RedirectResult;
            redirectResult.Url.Should().Be(model.ReturnUrl);
        }

        [Fact]
        public async Task Login_Post_ReturnsRedirectToHome_WhenReturnUrlIsNotLocal()
        {
            // Arrange
            var model = new LoginView
            {
                Username = "testuser",
                Password = "Test123!",
                ReturnUrl = "http://external.com"
            };

            var user = new User
            {
                UserName = model.Username,
                Role = "User"
            };

            _mockUserManager.Setup(um => um.FindByNameAsync(model.Username))
                .ReturnsAsync(user);
            _mockUserManager.Setup(um => um.CheckPasswordAsync(user, model.Password))
                .ReturnsAsync(true);
            _mockSignInManager.Setup(sm => sm.SignInAsync(user, true, null))
                .Returns(Task.CompletedTask);

            _controller.Url = new Mock<IUrlHelper>().Object;
            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(x => x.IsLocalUrl(model.ReturnUrl)).Returns(false);
            _controller.Url = mockUrlHelper.Object;

            _mockAuthService.Setup(auth => auth.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Login(model);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirectResult = result as RedirectToActionResult;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Home");
        }

        [Fact]
        public async Task Login_Post_AddsAdminRole_WhenUserIsAdmin()
        {
            // Arrange
            var model = new LoginView
            {
                Username = "admin",
                Password = "Admin123!",
                ReturnUrl = "/admin/dashboard"
            };

            var user = new User
            {
                UserName = model.Username,
                Role = "Admin"
            };

            ClaimsPrincipal capturedPrincipal = null;

            _mockUserManager.Setup(um => um.FindByNameAsync(model.Username))
                .ReturnsAsync(user);
            _mockUserManager.Setup(um => um.CheckPasswordAsync(user, model.Password))
                .ReturnsAsync(true);
            _mockSignInManager.Setup(sm => sm.SignInAsync(user, true, null))
                .Returns(Task.CompletedTask);

            _mockAuthService.Setup(auth => auth.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()))
                .Callback<HttpContext, string, ClaimsPrincipal, AuthenticationProperties>(
                    (ctx, scheme, principal, props) => capturedPrincipal = principal)
                .Returns(Task.CompletedTask);

            // Act
            await _controller.Login(model);

            // Assert
            capturedPrincipal.Should().NotBeNull();
            capturedPrincipal.HasClaim(ClaimTypes.Role, "Admin").Should().BeTrue();
            capturedPrincipal.HasClaim(ClaimTypes.Role, "User").Should().BeTrue();
        }

        [Fact]
        public async Task Login_Post_ReturnsView_WhenCredentialsAreInvalid()
        {
            // Arrange
            var model = new LoginView
            {
                Username = "testuser",
                Password = "WrongPassword",
                ReturnUrl = "/home/index"
            };

            var user = new User { UserName = model.Username };

            _mockUserManager.Setup(um => um.FindByNameAsync(model.Username))
                .ReturnsAsync(user);
            _mockUserManager.Setup(um => um.CheckPasswordAsync(user, model.Password))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Login(model);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().Be(model);
            _controller.ModelState.ErrorCount.Should().Be(1);
            _controller.ModelState.Should().ContainKey("");
            _controller.ModelState[""].Errors[0].ErrorMessage.Should().Be("Invalid login attempt.");
        }

        [Fact]
        public async Task Login_Post_ReturnsView_WhenModelStateIsInvalid()
        {
            // Arrange
            var model = new LoginView
            {
                Username = "",
                Password = "",
                ReturnUrl = "/home/index"
            };
            _controller.ModelState.AddModelError("Username", "Username is required");

            // Act
            var result = await _controller.Login(model);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().Be(model);
            viewResult.ViewData["ReturnUrl"].Should().Be(model.ReturnUrl);
        }

        #endregion

        #region Logout GET Tests

        [Fact]
        public void Logout_Get_ReturnsViewResult()
        {
            // Act
            var result = _controller.Logout();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        #endregion

        #region LogoutConfirmed POST Tests

        [Fact]
        public async Task LogoutConfirmed_Post_SignsOutAndRedirectsToHome()
        {
            // Arrange
            _mockSignInManager.Setup(sm => sm.SignOutAsync())
                .Returns(Task.CompletedTask);
            
            _mockAuthService.Setup(auth => auth.SignOutAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.LogoutConfirmed();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirectResult = result as RedirectToActionResult;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Home");

            _mockSignInManager.Verify(sm => sm.SignOutAsync(), Times.Once);
            _mockAuthService.Verify(auth => auth.SignOutAsync(
                It.IsAny<HttpContext>(),
                "Cookies",
                It.IsAny<AuthenticationProperties>()), Times.Once);
        }

        #endregion
    }
}