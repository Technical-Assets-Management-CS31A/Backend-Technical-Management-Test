using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs.Archive.Users;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11i — ArchiveUsersController
    /// Max per-test: 200 ms | IArchiveUserService fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class ArchiveUsersControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IArchiveUserService> _mockService;
        private readonly ArchiveUsersController    _sut;

        public ArchiveUsersControllerTests()
        {
            _mockService = new Mock<IArchiveUserService>();
            _sut         = new ArchiveUsersController(_mockService.Object);
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ArchiveUserDto MakeArchiveUserDto(Guid? id = null) => new()
        {
            Id        = id ?? Guid.NewGuid(),
            Username  = "jdelacruz",
            Email     = "juan@dlsu.edu.ph",
            LastName  = "Dela Cruz",
            FirstName = "Juan",
            UserRole  = UserRole.Student
        };

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetArchivedUserById_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.GetArchivedUserByIdAsync(id)).ReturnsAsync((ArchiveUserDto?)null);

            // Act
            var result = await _sut.GetArchivedUserById(id);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetArchivedUserById_Returns_Ok_WhenFound()
        {
            // Arrange
            var id  = Guid.NewGuid();
            var dto = MakeArchiveUserDto(id);
            _mockService.Setup(s => s.GetArchivedUserByIdAsync(id)).ReturnsAsync(dto);

            // Act
            var result = await _sut.GetArchivedUserById(id);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result).Value as ApiResponse<ArchiveUserDto>;
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task RestoreUser_Returns_BadRequest_WhenServiceReturnsFalse()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.RestoreUserAsync(id)).ReturnsAsync(false);

            // Act
            var result = await _sut.RestoreUser(id);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task PermanentDeleteUser_Returns_NotFound_WhenNotFound()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.PermanentDeleteArchivedUserAsync(id)).ReturnsAsync(false);

            // Act
            var result = await _sut.PermanentDeleteUser(id);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}
