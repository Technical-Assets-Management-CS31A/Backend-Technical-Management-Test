using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs.Item;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Services;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11c — ItemController
    /// Max per-test: 200 ms | IItemService fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class ItemControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IItemService> _mockItemService;
        private readonly ItemController     _sut;

        public ItemControllerTests()
        {
            _mockItemService = new Mock<IItemService>();
            _sut             = new ItemController(_mockItemService.Object);
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ItemDto MakeItemDto(Guid? id = null) => new()
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
        public async Task CreateItem_Returns_Conflict_WhenDuplicateSerialNumber()
        {
            // Arrange
            _mockItemService
                .Setup(s => s.CreateItemAsync(It.IsAny<TechnicalAssetManagementApi.Dtos.Item.CreateItemsDto>()))
                .ThrowsAsync(new ItemService.DuplicateSerialNumberException("Duplicate serial number."));

            // Act
            var result = await _sut.CreateItem(new TechnicalAssetManagementApi.Dtos.Item.CreateItemsDto());

            // Assert
            result.Result.Should().BeOfType<ConflictObjectResult>();
        }

        [Fact]
        public async Task CreateItem_Returns_BadRequest_WhenArgumentException()
        {
            // Arrange
            _mockItemService
                .Setup(s => s.CreateItemAsync(It.IsAny<TechnicalAssetManagementApi.Dtos.Item.CreateItemsDto>()))
                .ThrowsAsync(new ArgumentException("Serial number is required."));

            // Act
            var result = await _sut.CreateItem(new TechnicalAssetManagementApi.Dtos.Item.CreateItemsDto());

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateItem_Returns_201_OnSuccess()
        {
            // Arrange
            var itemDto = MakeItemDto();
            _mockItemService
                .Setup(s => s.CreateItemAsync(It.IsAny<TechnicalAssetManagementApi.Dtos.Item.CreateItemsDto>()))
                .ReturnsAsync(itemDto);

            // Act
            var result = await _sut.CreateItem(new TechnicalAssetManagementApi.Dtos.Item.CreateItemsDto());

            // Assert
            result.Result.Should().BeOfType<CreatedAtActionResult>();
            var created = (CreatedAtActionResult)result.Result!;
            created.StatusCode.Should().Be(201);
            var body = created.Value as ApiResponse<ItemDto>;
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task GetItemById_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockItemService.Setup(s => s.GetItemByIdAsync(id)).ReturnsAsync((ItemDto?)null);

            // Act
            var result = await _sut.GetItemById(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetItemById_Returns_Ok_WhenFound()
        {
            // Arrange
            var id      = Guid.NewGuid();
            var itemDto = MakeItemDto(id);
            _mockItemService.Setup(s => s.GetItemByIdAsync(id)).ReturnsAsync(itemDto);

            // Act
            var result = await _sut.GetItemById(id);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<ItemDto>;
            body!.Success.Should().BeTrue();
            body.Data!.Id.Should().Be(id);
        }

        [Fact]
        public async Task ScanRfid_Returns_NotFound_WhenNoItemLinked()
        {
            // Arrange
            _mockItemService
                .Setup(s => s.ScanRfidAsync("RFID-001"))
                .ReturnsAsync((false, "No item linked to this RFID.", (ItemStatus?)null));

            // Act
            var result = await _sut.ScanRfid("RFID-001");

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task ArchiveItem_Returns_NotFound_WhenItemNotFound()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockItemService
                .Setup(s => s.DeleteItemAsync(id))
                .ReturnsAsync((false, "Item not found."));

            // Act
            var result = await _sut.ArchiveItem(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task ArchiveItem_Returns_Ok_OnSuccess()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockItemService
                .Setup(s => s.DeleteItemAsync(id))
                .ReturnsAsync((true, string.Empty));

            // Act
            var result = await _sut.ArchiveItem(id);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<object>;
            body!.Success.Should().BeTrue();
        }
    }
}
