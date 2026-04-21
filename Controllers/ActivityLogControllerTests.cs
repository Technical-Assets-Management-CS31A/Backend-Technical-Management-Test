using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs.ActivityLog;
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
    /// Part 11e — ActivityLogController
    /// Max per-test: 200 ms | IActivityLogService fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class ActivityLogControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IActivityLogService> _mockService;
        private readonly ActivityLogController     _sut;

        public ActivityLogControllerTests()
        {
            _mockService = new Mock<IActivityLogService>();
            _sut         = new ActivityLogController(_mockService.Object);
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ActivityLogDto MakeLogDto(Guid? id = null) => new()
        {
            Id       = id ?? Guid.NewGuid(),
            Category = ActivityLogCategory.BorrowedItem,
            Action   = "Item borrowed"
        };

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_Returns_Ok_WithLogs()
        {
            // Arrange
            var logs = new List<ActivityLogDto> { MakeLogDto(), MakeLogDto(), MakeLogDto() };
            _mockService
                .Setup(s => s.GetAllAsync(It.IsAny<ActivityLogFilterDto>()))
                .ReturnsAsync(logs);

            // Act
            var result = await _sut.GetAll(null, null, null, null, null, null);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<IEnumerable<ActivityLogDto>>;
            body!.Success.Should().BeTrue();
            body.Data.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetById_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ActivityLogDto?)null);

            // Act
            var result = await _sut.GetById(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetById_Returns_Ok_WhenFound()
        {
            // Arrange
            var id  = Guid.NewGuid();
            var dto = MakeLogDto(id);
            _mockService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(dto);

            // Act
            var result = await _sut.GetById(id);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<ActivityLogDto>;
            body!.Success.Should().BeTrue();
            body.Data!.Id.Should().Be(id);
        }

        [Fact]
        public async Task GetBorrowLogs_Returns_Ok_WithLogs()
        {
            // Arrange
            var logs = new List<BorrowLogDto> { new(), new() };
            _mockService
                .Setup(s => s.GetBorrowLogsAsync(null, null, null, null))
                .ReturnsAsync(logs);

            // Act
            var result = await _sut.GetBorrowLogs(null, null, null, null);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<IEnumerable<BorrowLogDto>>;
            body!.Success.Should().BeTrue();
            body.Data.Should().HaveCount(2);
        }
    }
}
