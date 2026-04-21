using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs.User;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Models.DTOs.Users;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11a — AuthController
    /// Max per-test: 200 ms | All services fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService>        _mockAuthService;
        private readonly Mock<IUserService>        _mockUserService;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly AuthController _sut;

        public AuthControllerTests()
        {
            _mockAuthService = new Mock<IAuthService>();
            _mockUserService = new Mock<IUserService>();
            _mockEnv         = new Mock<IWebHostEnvironment>();
            _mockLogger      = new Mock<ILogger<AuthController>>();

            _sut = new AuthController(
                _mockAuthService.Object,
                _mockEnv.Object,
                _mockLogger.Object,
                _mockUserService.Object);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ClaimsPrincipal MakeUser(string role, Guid? id = null) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, (id ?? Guid.NewGuid()).ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "Test"));

        private static ClaimsPrincipal MakeUnauthenticated() =>
            new(new ClaimsIdentity());

        private void SetUser(ClaimsPrincipal user)
        {
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetMyProfile_Returns_Unauthorized_WhenClaimMissing()
        {
            // Arrange
            SetUser(MakeUnauthenticated());

            // Act
            var result = await _sut.GetMyProfile();

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
            var body = ((UnauthorizedObjectResult)result.Result!).Value as ApiResponse<object>;
            body!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task GetMyProfile_Returns_NotFound_WhenUserProfileNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetUser(MakeUser("Student", userId));
            _mockUserService.Setup(s => s.GetUserProfileByIdAsync(userId))
                            .ReturnsAsync((BaseProfileDto?)null);

            // Act
            var result = await _sut.GetMyProfile();

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetMyProfile_Returns_Ok_WithProfile()
        {
            // Arrange
            var userId  = Guid.NewGuid();
            var profile = new BaseProfileDto { Id = userId, FirstName = "Juan" };
            SetUser(MakeUser("Student", userId));
            _mockUserService.Setup(s => s.GetUserProfileByIdAsync(userId))
                            .ReturnsAsync(profile);

            // Act
            var result = await _sut.GetMyProfile();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<object>;
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task Register_Returns_Unauthorized_WhenClaimInvalid()
        {
            // Arrange — principal with a non-parseable NameIdentifier
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "Test"));
            SetUser(principal);

            // Act
            var result = await _sut.Register(new RegisterUserDto { Username = "test" });

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task Register_Returns_201_OnSuccess()
        {
            // Arrange
            var callerId = Guid.NewGuid();
            var newUser  = new UserDto { Id = Guid.NewGuid(), Username = "jdelacruz" };
            SetUser(MakeUser("Admin", callerId));
            _mockAuthService.Setup(s => s.Register(It.IsAny<RegisterUserDto>(), callerId))
                            .ReturnsAsync(newUser);

            // Act
            var result = await _sut.Register(new RegisterUserDto { Username = "jdelacruz" });

            // Assert
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(201);
            var body = objectResult.Value as ApiResponse<UserDto>;
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task Login_Returns_Ok_WithUserDto()
        {
            // Arrange
            var userDto = new UserDto { Id = Guid.NewGuid(), Email = "juan@dlsu.edu.ph" };
            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            _mockAuthService.Setup(s => s.Login(It.IsAny<LoginUserDto>()))
                            .ReturnsAsync(userDto);
            SetUser(MakeUnauthenticated());

            // Act
            var result = await _sut.Login(new LoginUserDto());

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<UserDto>;
            body!.Data.Should().NotBeNull();
            body.Success.Should().BeTrue();
        }

        [Fact]
        public async Task ChangePassword_Returns_Ok_OnSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetUser(MakeUser("Student", userId));
            _mockAuthService.Setup(s => s.ChangePassword(userId, It.IsAny<ChangePasswordDto>()))
                            .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.ChangePassword(userId, new ChangePasswordDto());

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<object>;
            body!.Success.Should().BeTrue();
        }
    }
}
