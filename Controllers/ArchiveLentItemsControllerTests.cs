using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs.Archive.LentItems;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11h — ArchiveLentItemsController
    /// Max per-test: 200 ms | IArchiveLentItemsService fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class ArchiveLentItemsControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IArchiveLentItemsService>              _mockService;
        private readonly Mock<ILogger<ArchiveLentItemsController>>   _mockLogger;
        private readonly ArchiveLentItemsController                  _sut;

        public ArchiveLentItemsControllerTests()
        {
            _mockService = new Mock<IArchiveLentItemsService>();
            _mockLogger  = new Mock<ILogger<ArchiveLentItemsController>>();
            _sut         = new ArchiveLentItemsController(_mockService.Object, _mockLogger.Object);
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ArchiveLentItemsDto MakeArchiveLentItemDto(Guid? id = null) => new()
        {
            Id             = id ?? Guid.NewGuid(),
            BorrowerRole   = "Student",
            BorrowerFullName = "Juan Dela Cruz",
            Status         = "Returned"
        };

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetLentItemsArchiveById_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.GetLentItemsArchiveByIdAsync(id)).ReturnsAsync((ArchiveLentItemsDto?)null);

            // Act
            var result = await _sut.GetLentItemsArchiveById(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetLentItemsArchiveById_Returns_Ok_WhenFound()
        {
            // Arrange
            var id  = Guid.NewGuid();
            var dto = MakeArchiveLentItemDto(id);
            _mockService.Setup(s => s.GetLentItemsArchiveByIdAsync(id)).ReturnsAsync(dto);

            // Act
            var result = await _sut.GetLentItemsArchiveById(id);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<ArchiveLentItemsDto>;
            body!.Success.Should().BeTrue();
            body.Data!.Id.Should().Be(id);
        }

        [Fact]
        public async Task RestoreArchivedLentItems_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.RestoreLentItemsAsync(id)).ReturnsAsync((ArchiveLentItemsDto?)null);

            // Act
            var result = await _sut.RestoreArchivedLentItems(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task DeleteLentItemsArchive_Returns_NotFound_WhenNotFound()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.DeleteLentItemsArchiveAsync(id)).ReturnsAsync(false);

            // Act
            var result = await _sut.DeleteLentItemsArchive(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}
