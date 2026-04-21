using AutoMapper;
using BackendTechnicalAssetsManagement.src.Authorization;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs.User;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Models.DTOs.Users;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using static BackendTechnicalAssetsManagement.src.DTOs.User.UserProfileDtos;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11b — UserController
    /// Max per-test: 200 ms | All services fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class UserControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IUserService>          _mockUserService;
        private readonly Mock<IUserRepository>       _mockUserRepo;
        private readonly Mock<IMapper>               _mockMapper;
        private readonly Mock<IAuthorizationService> _mockAuthorizationService;
        private readonly UserController              _sut;

        public UserControllerTests()
        {
            _mockUserService          = new Mock<IUserService>();
            _mockUserRepo             = new Mock<IUserRepository>();
            _mockMapper               = new Mock<IMapper>();
            _mockAuthorizationService = new Mock<IAuthorizationService>();

            _sut = new UserController(
                _mockUserService.Object,
                _mockUserRepo.Object,
                _mockMapper.Object,
                _mockAuthorizationService.Object);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ClaimsPrincipal MakeUser(string role, Guid? id = null) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, (id ?? Guid.NewGuid()).ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "Test"));

        private void SetUser(ClaimsPrincipal user)
        {
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        private static User MakeUserEntity(Guid? id = null) => new Student
        {
            Id        = id ?? Guid.NewGuid(),
            Username  = "jdelacruz",
            Email     = "juan@dlsu.edu.ph",
            LastName  = "Dela Cruz",
            FirstName = "Juan"
        };

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsers_Returns_Ok_WithUserList()
        {
            // Arrange
            var users = new List<UserDto>
            {
                new() { Id = Guid.NewGuid(), Username = "user1" },
                new() { Id = Guid.NewGuid(), Username = "user2" }
            };
            SetUser(MakeUser("Admin"));
            _mockUserService.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(users);

            // Act
            var result = await _sut.GetAllUsers();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<IEnumerable<object>>;
            body!.Success.Should().BeTrue();
            body.Data.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetUserProfileById_Returns_NotFound_WhenUserNotInRepo()
        {
            // Arrange
            var id = Guid.NewGuid();
            SetUser(MakeUser("Admin"));
            _mockUserRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((User?)null);

            // Act
            var result = await _sut.GetUserProfileById(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetUserProfileById_Returns_Forbidden_WhenAuthorizationFails()
        {
            // Arrange
            var id   = Guid.NewGuid();
            var user = MakeUserEntity(id);
            SetUser(MakeUser("Student"));
            _mockUserRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(user);
            // AuthorizeAsync extension calls the interface method with IEnumerable<IAuthorizationRequirement>
            _mockAuthorizationService
                .Setup(a => a.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Failed());

            // Act
            var result = await _sut.GetUserProfileById(id);

            // Assert
            result.Result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task GetUserProfileById_Returns_Ok_WhenAuthorized()
        {
            // Arrange
            var id      = Guid.NewGuid();
            var user    = MakeUserEntity(id);
            var profile = new BaseProfileDto { Id = id, FirstName = "Juan" };
            SetUser(MakeUser("Admin"));
            _mockUserRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(user);
            // AuthorizeAsync extension calls the interface method with IEnumerable<IAuthorizationRequirement>
            _mockAuthorizationService
                .Setup(a => a.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());
            _mockUserService.Setup(s => s.GetUserProfileByIdAsync(id)).ReturnsAsync(profile);

            // Act
            var result = await _sut.GetUserProfileById(id);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<object>;
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateStudentProfile_Returns_Unauthorized_WhenClaimInvalid()
        {
            // Arrange — no NameIdentifier claim
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Role, "Student")
                    }, "Test"))
                }
            };

            // Act
            var result = await _sut.UpdateStudentProfile(Guid.NewGuid(), new UpdateStudentProfileDto());

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task UpdateStudentProfile_Returns_Forbidden_WhenStudentUpdatesOtherProfile()
        {
            // Arrange — caller is a Student but target ID differs
            var callerId = Guid.NewGuid();
            var targetId = Guid.NewGuid(); // different from callerId
            SetUser(MakeUser("Student", callerId));

            // Act
            var result = await _sut.UpdateStudentProfile(targetId, new UpdateStudentProfileDto());

            // Assert
            result.Result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task ArchiveUser_Returns_NotFound_WhenServiceReturnsNotFound()
        {
            // Arrange
            var callerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            SetUser(MakeUser("Admin", callerId));
            _mockUserService
                .Setup(s => s.DeleteUserAsync(targetId, callerId))
                .ReturnsAsync((false, "User not found."));

            // Act
            var result = await _sut.ArchiveUser(targetId);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task ArchiveUser_Returns_Ok_OnSuccess()
        {
            // Arrange
            var callerId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            SetUser(MakeUser("Admin", callerId));
            _mockUserService
                .Setup(s => s.DeleteUserAsync(targetId, callerId))
                .ReturnsAsync((true, string.Empty));

            // Act
            var result = await _sut.ArchiveUser(targetId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<object>;
            body!.Success.Should().BeTrue();
        }
    }
}
