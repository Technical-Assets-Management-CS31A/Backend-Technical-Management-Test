using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs.Archive.Items;
using BackendTechnicalAssetsManagement.src.DTOs.Item;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11g — ArchiveItemsController
    /// Max per-test: 200 ms | IArchiveItemsService fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class ArchiveItemsControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IArchiveItemsService>          _mockService;
        private readonly Mock<ILogger<ArchiveItemsController>> _mockLogger;
        private readonly ArchiveItemsController              _sut;

        public ArchiveItemsControllerTests()
        {
            _mockService = new Mock<IArchiveItemsService>();
            _mockLogger  = new Mock<ILogger<ArchiveItemsController>>();
            _sut         = new ArchiveItemsController(_mockService.Object, _mockLogger.Object);
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ArchiveItemsDto MakeArchiveItemDto(Guid? id = null) => new()
        {
            Id           = id ?? Guid.NewGuid(),
            SerialNumber = "SN-001",
            ItemName     = "Projector",
            ItemType     = "Electronics",
            ItemMake     = "Epson",
            Condition    = ItemCondition.Good,
            Status       = ItemStatus.Available
        };

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetArchivedItemById_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.GetItemArchiveByIdAsync(id)).ReturnsAsync((ArchiveItemsDto?)null);

            // Act
            var result = await _sut.GetArchivedItemById(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetArchivedItemById_Returns_Ok_WhenFound()
        {
            // Arrange
            var id  = Guid.NewGuid();
            var dto = MakeArchiveItemDto(id);
            _mockService.Setup(s => s.GetItemArchiveByIdAsync(id)).ReturnsAsync(dto);

            // Act
            var result = await _sut.GetArchivedItemById(id);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<ArchiveItemsDto>;
            body!.Success.Should().BeTrue();
            body.Data!.Id.Should().Be(id);
        }

        [Fact]
        public async Task RestoreArchivedItem_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.RestoreItemAsync(id)).ReturnsAsync((ItemDto?)null);

            // Act
            var result = await _sut.RestoreArchivedItem(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task DeleteArchivedItem_Returns_NotFound_WhenNotFound()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockService.Setup(s => s.DeleteItemArchiveAsync(id)).ReturnsAsync(false);

            // Act
            var result = await _sut.DeleteArchivedItem(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}
