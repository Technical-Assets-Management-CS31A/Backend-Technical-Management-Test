using AutoMapper;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs.Archive.Items;
using BackendTechnicalAssetsManagement.src.DTOs.Item;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using TechnicalAssetManagementApi.Dtos.Item;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;
using static BackendTechnicalAssetsManagement.src.Services.ItemService;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 3 — Inventory & Items (ItemService)
    /// Covers: Create, Read, Update, Delete, RFID, Location
    /// All dependencies mocked — zero DB latency.
    /// </summary>
    public class ItemServiceTests
    {
        private readonly Mock<IItemRepository>         _mockItemRepo;
        private readonly Mock<IMapper>                 _mockMapper;
        private readonly Mock<IWebHostEnvironment>     _mockEnv;
        private readonly Mock<IArchiveItemsService>    _mockArchive;
        private readonly Mock<ILentItemsRepository>    _mockLentRepo;
        private readonly Mock<ISupabaseStorageService> _mockStorage;

        private readonly ItemService _sut;

        public ItemServiceTests()
        {
            _mockItemRepo = new Mock<IItemRepository>();
            _mockMapper   = new Mock<IMapper>();
            _mockEnv      = new Mock<IWebHostEnvironment>();
            _mockArchive  = new Mock<IArchiveItemsService>();
            _mockLentRepo = new Mock<ILentItemsRepository>();
            _mockStorage  = new Mock<ISupabaseStorageService>();

            _sut = new ItemService(
                _mockItemRepo.Object,
                _mockMapper.Object,
                _mockEnv.Object,
                _mockArchive.Object,
                _mockLentRepo.Object,
                _mockStorage.Object
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Item MakeItem(
            ItemCondition condition = ItemCondition.Good,
            ItemStatus status = ItemStatus.Available,
            string? rfid = null) => new()
        {
            Id           = Guid.NewGuid(),
            ItemName     = "Projector",
            SerialNumber = "SN-001",
            Condition    = condition,
            Status       = status,
            RfidUid      = rfid,
            Category     = ItemCategory.Electronics
        };

        private static CreateItemsDto ValidCreateDto(string serial = "001") => new()
        {
            ItemName  = "Projector",
            SerialNumber = serial,
            Condition = ItemCondition.Good,
            Category  = ItemCategory.Electronics,
            ItemType  = "Projector",
            ItemMake  = "Epson"
        };

        // ══════════════════════════════════════════════════════════════════════
        // CREATE ITEM
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CreateItem_Throws_When_SerialNumber_IsEmpty()
        {
            var dto = ValidCreateDto();
            dto.SerialNumber = "";

            Func<Task> act = () => _sut.CreateItemAsync(dto);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*SerialNumber*");
        }

        [Fact]
        public async Task CreateItem_Throws_When_DuplicateSerialNumber_Exists()
        {
            var dto = ValidCreateDto("SN-001");
            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-001"))
                         .ReturnsAsync(MakeItem());

            Func<Task> act = () => _sut.CreateItemAsync(dto);

            await act.Should().ThrowAsync<DuplicateSerialNumberException>();
        }

        [Fact]
        public async Task CreateItem_AutoPrefixes_SN_When_Missing()
        {
            var dto = ValidCreateDto("MYSERIAL");
            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-MYSERIAL")).ReturnsAsync((Item?)null);
            _mockItemRepo.Setup(r => r.AddAsync(It.IsAny<Item>())).ReturnsAsync(new Item());
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<Item>(It.IsAny<CreateItemsDto>())).Returns(new Item());
            _mockMapper.Setup(m => m.Map<ItemDto>(It.IsAny<Item>())).Returns(new ItemDto());

            await _sut.CreateItemAsync(dto);

            dto.SerialNumber.Should().Be("SN-MYSERIAL");
        }

        [Fact]
        public async Task CreateItem_NormalizesSerialNumber_ToUppercase()
        {
            var dto = ValidCreateDto("sn-lowercase");
            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-LOWERCASE")).ReturnsAsync((Item?)null);
            _mockItemRepo.Setup(r => r.AddAsync(It.IsAny<Item>())).ReturnsAsync(new Item());
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<Item>(It.IsAny<CreateItemsDto>())).Returns(new Item());
            _mockMapper.Setup(m => m.Map<ItemDto>(It.IsAny<Item>())).Returns(new ItemDto());

            await _sut.CreateItemAsync(dto);

            dto.SerialNumber.Should().Be("SN-LOWERCASE");
        }

        [Fact]
        public async Task CreateItem_DoesNotDuplicatePrefix_WhenSnAlreadyPresent()
        {
            var dto = ValidCreateDto("SN-ALREADY");
            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-ALREADY")).ReturnsAsync((Item?)null);
            _mockItemRepo.Setup(r => r.AddAsync(It.IsAny<Item>())).ReturnsAsync(new Item());
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<Item>(It.IsAny<CreateItemsDto>())).Returns(new Item());
            _mockMapper.Setup(m => m.Map<ItemDto>(It.IsAny<Item>())).Returns(new ItemDto());

            await _sut.CreateItemAsync(dto);

            dto.SerialNumber.Should().Be("SN-ALREADY");
        }

        [Fact]
        public async Task CreateItem_UploadsImage_WhenImageProvided()
        {
            var dto      = ValidCreateDto("SN-IMG");
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("photo.jpg");
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));
            dto.Image = mockFile.Object;

            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-IMG")).ReturnsAsync((Item?)null);
            _mockItemRepo.Setup(r => r.AddAsync(It.IsAny<Item>())).ReturnsAsync(new Item());
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockStorage.Setup(s => s.UploadImageAsync(It.IsAny<IFormFile>(), "items"))
                        .ReturnsAsync("https://cdn.example.com/photo.jpg");
            _mockMapper.Setup(m => m.Map<Item>(It.IsAny<CreateItemsDto>())).Returns(new Item());
            _mockMapper.Setup(m => m.Map<ItemDto>(It.IsAny<Item>())).Returns(new ItemDto());

            await _sut.CreateItemAsync(dto);

            _mockStorage.Verify(s => s.UploadImageAsync(It.IsAny<IFormFile>(), "items"), Times.Once);
        }

        [Fact]
        public async Task CreateItem_Returns_MappedDto_OnSuccess()
        {
            var dto      = ValidCreateDto("SN-NEW");
            var expected = new ItemDto { ItemName = "Projector" };

            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-NEW")).ReturnsAsync((Item?)null);
            _mockItemRepo.Setup(r => r.AddAsync(It.IsAny<Item>())).ReturnsAsync(new Item());
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<Item>(It.IsAny<CreateItemsDto>())).Returns(new Item());
            _mockMapper.Setup(m => m.Map<ItemDto>(It.IsAny<Item>())).Returns(expected);

            var result = await _sut.CreateItemAsync(dto);

            result.Should().BeEquivalentTo(expected);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET ITEMS
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAllItems_Returns_MappedCollection()
        {
            var items = new List<Item> { MakeItem(), MakeItem() };
            var dtos  = new List<ItemDto> { new(), new() };

            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(items);
            _mockMapper.Setup(m => m.Map<IEnumerable<ItemDto>>(items)).Returns(dtos);

            var result = await _sut.GetAllItemsAsync();

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetItemById_Returns_Null_When_NotFound()
        {
            _mockItemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Item?)null);
            _mockMapper.Setup(m => m.Map<ItemDto?>(null)).Returns((ItemDto?)null);

            var result = await _sut.GetItemByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetItemById_Returns_MappedDto_When_Found()
        {
            var item = MakeItem();
            var dto  = new ItemDto { ItemName = item.ItemName };

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockMapper.Setup(m => m.Map<ItemDto?>(item)).Returns(dto);

            var result = await _sut.GetItemByIdAsync(item.Id);

            result.Should().BeEquivalentTo(dto);
        }

        [Fact]
        public async Task GetItemBySerialNumber_Returns_Null_When_NotFound()
        {
            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>()))
                         .ReturnsAsync((Item?)null);
            _mockMapper.Setup(m => m.Map<ItemDto?>(null)).Returns((ItemDto?)null);

            var result = await _sut.GetItemBySerialNumberAsync("SN-MISSING");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetItemByRfid_Returns_Null_When_NotFound()
        {
            _mockItemRepo.Setup(r => r.GetByRfidUidAsync(It.IsAny<string>()))
                         .ReturnsAsync((Item?)null);
            _mockMapper.Setup(m => m.Map<ItemDto?>(null)).Returns((ItemDto?)null);

            var result = await _sut.GetItemByRfidUidAsync("UNKNOWN");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetItemByRfid_Returns_MappedDto_When_Found()
        {
            var item = MakeItem(rfid: "AABBCCDD");
            var dto  = new ItemDto { ItemName = item.ItemName };

            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync(item);
            _mockMapper.Setup(m => m.Map<ItemDto?>(item)).Returns(dto);

            var result = await _sut.GetItemByRfidUidAsync("AABBCCDD");

            result.Should().BeEquivalentTo(dto);
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE ITEM
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateItem_Returns_False_When_ItemNotFound()
        {
            _mockItemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Item?)null);

            var result = await _sut.UpdateItemAsync(Guid.NewGuid(), new UpdateItemsDto());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateItem_Returns_True_When_SaveSucceeds()
        {
            var item = MakeItem();
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.UpdateItemAsync(item.Id, new UpdateItemsDto());

            result.Should().BeTrue();
            _mockItemRepo.Verify(r => r.UpdateAsync(item), Times.Once);
        }

        [Fact]
        public async Task UpdateItem_Throws_When_NewSerialNumber_AlreadyExists_OnDifferentItem()
        {
            var item      = MakeItem();
            var otherItem = MakeItem();
            otherItem.SerialNumber = "SN-TAKEN";

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-TAKEN")).ReturnsAsync(otherItem);

            var dto = new UpdateItemsDto { SerialNumber = "TAKEN" };

            Func<Task> act = () => _sut.UpdateItemAsync(item.Id, dto);

            await act.Should().ThrowAsync<DuplicateSerialNumberException>();
        }

        [Fact]
        public async Task UpdateItem_DeletesOldImage_WhenNewImageProvided()
        {
            var item = MakeItem();
            item.ImageUrl = "https://cdn.example.com/old.jpg";

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("new.jpg");
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockStorage.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                        .Returns(Task.CompletedTask);
            _mockStorage.Setup(s => s.UploadImageAsync(It.IsAny<IFormFile>(), "items"))
                        .ReturnsAsync("https://cdn.example.com/new.jpg");

            var dto = new UpdateItemsDto { Image = mockFile.Object };
            await _sut.UpdateItemAsync(item.Id, dto);

            // Verify old image was deleted and new one uploaded
            _mockStorage.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Once);
            _mockStorage.Verify(s => s.UploadImageAsync(It.IsAny<IFormFile>(), "items"), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE ITEM LOCATION
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateItemLocation_Returns_Failure_When_ItemNotFound()
        {
            _mockItemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Item?)null);

            var (success, error) = await _sut.UpdateItemLocationAsync(Guid.NewGuid(), "Room 101");

            success.Should().BeFalse();
            error.Should().Be("Item not found.");
        }

        [Fact]
        public async Task UpdateItemLocation_Returns_Success_When_ItemExists()
        {
            var item = MakeItem();
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var (success, error) = await _sut.UpdateItemLocationAsync(item.Id, "Room 101");

            success.Should().BeTrue();
            error.Should().BeEmpty();
            item.Location.Should().Be("Room 101");
        }

        // ══════════════════════════════════════════════════════════════════════
        // DELETE ITEM (archive)
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task DeleteItem_Returns_Failure_When_ItemNotFound()
        {
            _mockItemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Item?)null);

            var (success, error) = await _sut.DeleteItemAsync(Guid.NewGuid());

            success.Should().BeFalse();
            error.Should().Be("Item not found.");
        }

        [Fact]
        public async Task DeleteItem_Returns_Success_When_ArchiveAndDeleteSucceed()
        {
            var item = MakeItem();
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockMapper.Setup(m => m.Map<CreateArchiveItemsDto>(item))
                       .Returns(new CreateArchiveItemsDto());
            _mockArchive.Setup(s => s.CreateItemArchiveAsync(It.IsAny<CreateArchiveItemsDto>()))
                        .ReturnsAsync(new ArchiveItemsDto());
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var (success, error) = await _sut.DeleteItemAsync(item.Id);

            success.Should().BeTrue();
            error.Should().BeEmpty();
        }

        // ══════════════════════════════════════════════════════════════════════
        // SCAN RFID
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ScanRfid_Returns_Failure_When_NoItemLinked()
        {
            _mockItemRepo.Setup(r => r.GetByRfidUidAsync(It.IsAny<string>()))
                         .ReturnsAsync((Item?)null);

            var (success, error, _) = await _sut.ScanRfidAsync("UNKNOWN");

            success.Should().BeFalse();
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ScanRfid_Returns_Success_When_ItemFound_WithActiveLentRecord()
        {
            var item    = MakeItem(status: ItemStatus.Available, rfid: "AABBCCDD");
            var lentRec = new LentItems
            {
                Id     = Guid.NewGuid(),
                ItemId = item.Id,
                Status = "Borrowed"
            };

            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync(item);
            _mockLentRepo.Setup(r => r.GetActiveByItemIdAsync(item.Id)).ReturnsAsync(lentRec);
            _mockLentRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var (success, _, _) = await _sut.ScanRfidAsync("AABBCCDD");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task ScanRfid_Returns_Failure_When_NoActiveLentRecord()
        {
            var item = MakeItem(rfid: "AABBCCDD");

            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync(item);
            _mockLentRepo.Setup(r => r.GetActiveByItemIdAsync(item.Id))
                         .ReturnsAsync((LentItems?)null);

            var (success, error, _) = await _sut.ScanRfidAsync("AABBCCDD");

            success.Should().BeFalse();
            error.Should().Contain("No active lent record");
        }

        // ══════════════════════════════════════════════════════════════════════
        // REGISTER RFID TO ITEM
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task RegisterRfidToItem_Returns_Failure_When_ItemNotFound()
        {
            // RFID not taken by another item, but RegisterRfidAsync returns null (item not found)
            _mockItemRepo.Setup(r => r.GetByRfidUidAsync(It.IsAny<string>()))
                         .ReturnsAsync((Item?)null);
            _mockItemRepo.Setup(r => r.RegisterRfidAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                         .ReturnsAsync((Item?)null);

            var (success, error) = await _sut.RegisterRfidToItemAsync(Guid.NewGuid(), "AABBCCDD");

            success.Should().BeFalse();
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RegisterRfidToItem_Returns_Failure_When_RfidAlreadyTakenByAnotherItem()
        {
            var existingItem = MakeItem(rfid: "AABBCCDD");
            var targetId     = Guid.NewGuid(); // different item

            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync(existingItem);

            var (success, error) = await _sut.RegisterRfidToItemAsync(targetId, "AABBCCDD");

            success.Should().BeFalse();
            error.Should().Contain("already registered");
        }

        [Fact]
        public async Task RegisterRfidToItem_Returns_Success_When_Registered()
        {
            var item = MakeItem(rfid: "AABBCCDD");

            // RFID not taken by any other item
            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync((Item?)null);
            _mockItemRepo.Setup(r => r.RegisterRfidAsync(item.Id, "AABBCCDD")).ReturnsAsync(item);
            _mockItemRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var (success, error) = await _sut.RegisterRfidToItemAsync(item.Id, "AABBCCDD");

            success.Should().BeTrue();
            error.Should().BeEmpty();
        }
    }
}
