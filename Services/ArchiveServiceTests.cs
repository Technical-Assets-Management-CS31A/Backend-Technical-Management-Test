using AutoMapper;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs.Archive.Items;
using BackendTechnicalAssetsManagement.src.DTOs.Archive.LentItems;
using BackendTechnicalAssetsManagement.src.DTOs.Item;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 7 — Archival Services
    /// Covers: ArchiveItemsService and ArchiveLentItemsService
    /// ArchiveUserService is excluded — it uses AppDbContext transactions directly
    /// and requires integration-level testing.
    /// All dependencies mocked — zero DB latency.
    /// </summary>

    // ══════════════════════════════════════════════════════════════════════════
    // ARCHIVE ITEMS SERVICE
    // ══════════════════════════════════════════════════════════════════════════

    public class ArchiveItemsServiceTests
    {
        private readonly Mock<IArchiveItemRepository> _mockArchiveRepo;
        private readonly Mock<IItemRepository>        _mockItemRepo;
        private readonly Mock<IMapper>                _mockMapper;

        private readonly ArchiveItemsService _sut;

        public ArchiveItemsServiceTests()
        {
            _mockArchiveRepo = new Mock<IArchiveItemRepository>();
            _mockItemRepo    = new Mock<IItemRepository>();
            _mockMapper      = new Mock<IMapper>();

            _sut = new ArchiveItemsService(
                _mockArchiveRepo.Object,
                _mockItemRepo.Object,
                _mockMapper.Object
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ArchiveItems MakeArchiveItem(Guid? id = null) => new()
        {
            Id           = id ?? Guid.NewGuid(),
            ItemName     = "Projector",
            SerialNumber = "SN-001"
        };

        // ── CreateItemArchiveAsync ─────────────────────────────────────────────

        [Fact]
        public async Task CreateItemArchive_Maps_And_Saves_To_Archive()
        {
            var dto          = new CreateArchiveItemsDto { ItemName = "Projector" };
            var archiveItem  = MakeArchiveItem();
            var expectedDto  = new ArchiveItemsDto { ItemName = "Projector" };

            _mockMapper.Setup(m => m.Map<ArchiveItems>(dto)).Returns(archiveItem);
            _mockMapper.Setup(m => m.Map<ArchiveItemsDto>(archiveItem)).Returns(expectedDto);

            var result = await _sut.CreateItemArchiveAsync(dto);

            result.Should().BeEquivalentTo(expectedDto);
            _mockArchiveRepo.Verify(r => r.CreateItemArchiveAsync(archiveItem), Times.Once);
        }

        // ── GetItemArchiveByIdAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetItemArchiveById_Returns_Null_When_NotFound()
        {
            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ArchiveItems?)null);
            _mockMapper.Setup(m => m.Map<ArchiveItemsDto?>(null)).Returns((ArchiveItemsDto?)null);

            var result = await _sut.GetItemArchiveByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetItemArchiveById_Returns_MappedDto_When_Found()
        {
            var archiveItem = MakeArchiveItem();
            var dto         = new ArchiveItemsDto { ItemName = archiveItem.ItemName };

            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockMapper.Setup(m => m.Map<ArchiveItemsDto?>(archiveItem)).Returns(dto);

            var result = await _sut.GetItemArchiveByIdAsync(archiveItem.Id);

            result.Should().BeEquivalentTo(dto);
        }

        // ── DeleteItemArchiveAsync ─────────────────────────────────────────────

        [Fact]
        public async Task DeleteItemArchive_Returns_False_When_NotFound()
        {
            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ArchiveItems?)null);

            var result = await _sut.DeleteItemArchiveAsync(Guid.NewGuid());

            result.Should().BeFalse();
            _mockArchiveRepo.Verify(r => r.DeleteItemArchiveAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task DeleteItemArchive_Returns_True_When_Deleted()
        {
            var archiveItem = MakeArchiveItem();

            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockArchiveRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.DeleteItemArchiveAsync(archiveItem.Id);

            result.Should().BeTrue();
            _mockArchiveRepo.Verify(r => r.DeleteItemArchiveAsync(archiveItem.Id), Times.Once);
        }

        // ── RestoreItemAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task RestoreItem_Returns_Null_When_ArchiveNotFound()
        {
            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ArchiveItems?)null);

            var result = await _sut.RestoreItemAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task RestoreItem_Restores_Item_And_Returns_Dto()
        {
            var archiveItem  = MakeArchiveItem();
            var restoredItem = new Item { Id = archiveItem.Id, ItemName = archiveItem.ItemName };
            var dto          = new ItemDto { ItemName = archiveItem.ItemName };

            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockMapper.Setup(m => m.Map<Item>(archiveItem)).Returns(restoredItem);
            _mockMapper.Setup(m => m.Map<ItemDto>(restoredItem)).Returns(dto);
            _mockArchiveRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.RestoreItemAsync(archiveItem.Id);

            result.Should().BeEquivalentTo(dto);
            _mockItemRepo.Verify(r => r.AddAsync(restoredItem), Times.Once);
            _mockArchiveRepo.Verify(r => r.DeleteItemArchiveAsync(archiveItem.Id), Times.Once);
        }

        [Fact]
        public async Task RestoreItem_Sets_Status_To_Available()
        {
            var archiveItem  = MakeArchiveItem();
            var restoredItem = new Item { Id = archiveItem.Id, Status = ItemStatus.Archived };

            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockMapper.Setup(m => m.Map<Item>(archiveItem)).Returns(restoredItem);
            _mockMapper.Setup(m => m.Map<ItemDto>(restoredItem)).Returns(new ItemDto());
            _mockArchiveRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            await _sut.RestoreItemAsync(archiveItem.Id);

            restoredItem.Status.Should().Be(ItemStatus.Available);
        }

        // ── UpdateItemArchiveAsync ─────────────────────────────────────────────

        [Fact]
        public async Task UpdateItemArchive_Returns_False_When_NotFound()
        {
            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ArchiveItems?)null);

            var result = await _sut.UpdateItemArchiveAsync(Guid.NewGuid(), new UpdateArchiveItemsDto());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateItemArchive_Returns_True_When_SaveSucceeds()
        {
            var archiveItem = MakeArchiveItem();

            _mockArchiveRepo.Setup(r => r.GetItemArchiveByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockArchiveRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.UpdateItemArchiveAsync(archiveItem.Id, new UpdateArchiveItemsDto());

            result.Should().BeTrue();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ARCHIVE LENT ITEMS SERVICE
    // ══════════════════════════════════════════════════════════════════════════

    public class ArchiveLentItemsServiceTests
    {
        private readonly Mock<IArchiveLentItemsRepository> _mockArchiveRepo;
        private readonly Mock<ILentItemsRepository>        _mockLentRepo;
        private readonly Mock<IMapper>                     _mockMapper;

        private readonly ArchiveLentItemsService _sut;

        public ArchiveLentItemsServiceTests()
        {
            _mockArchiveRepo = new Mock<IArchiveLentItemsRepository>();
            _mockLentRepo    = new Mock<ILentItemsRepository>();
            _mockMapper      = new Mock<IMapper>();

            _sut = new ArchiveLentItemsService(
                _mockArchiveRepo.Object,
                _mockLentRepo.Object,
                _mockMapper.Object
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ArchiveLentItems MakeArchiveLentItem(Guid? id = null) => new()
        {
            Id       = id ?? Guid.NewGuid(),
            ItemName = "Projector",
            Status   = "Returned"
        };

        // ── CreateLentItemsArchiveAsync ────────────────────────────────────────

        [Fact]
        public async Task CreateLentItemsArchive_Maps_And_Saves()
        {
            var dto         = new CreateArchiveLentItemsDto { BorrowerFullName = "Juan" };
            var archiveItem = MakeArchiveLentItem();
            var expectedDto = new ArchiveLentItemsDto { BorrowerFullName = "Juan" };

            _mockMapper.Setup(m => m.Map<ArchiveLentItems>(dto)).Returns(archiveItem);
            _mockMapper.Setup(m => m.Map<ArchiveLentItemsDto>(archiveItem)).Returns(expectedDto);

            var result = await _sut.CreateLentItemsArchiveAsync(dto);

            result.Should().BeEquivalentTo(expectedDto);
            _mockArchiveRepo.Verify(r => r.CreateArchiveLentItemsAsync(archiveItem), Times.Once);
        }

        // ── GetLentItemsArchiveByIdAsync ───────────────────────────────────────

        [Fact]
        public async Task GetLentItemsArchiveById_Returns_Null_When_NotFound()
        {
            _mockArchiveRepo.Setup(r => r.GetArchiveLentItemsByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ArchiveLentItems?)null);
            _mockMapper.Setup(m => m.Map<ArchiveLentItemsDto?>(null))
                       .Returns((ArchiveLentItemsDto?)null);

            var result = await _sut.GetLentItemsArchiveByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetLentItemsArchiveById_Returns_MappedDto_When_Found()
        {
            var archiveItem = MakeArchiveLentItem();
            var dto         = new ArchiveLentItemsDto { BorrowerFullName = "Juan" };

            _mockArchiveRepo.Setup(r => r.GetArchiveLentItemsByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockMapper.Setup(m => m.Map<ArchiveLentItemsDto?>(archiveItem)).Returns(dto);

            var result = await _sut.GetLentItemsArchiveByIdAsync(archiveItem.Id);

            result.Should().BeEquivalentTo(dto);
        }

        // ── DeleteLentItemsArchiveAsync ────────────────────────────────────────

        [Fact]
        public async Task DeleteLentItemsArchive_Returns_False_When_NotFound()
        {
            _mockArchiveRepo.Setup(r => r.GetArchiveLentItemsByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ArchiveLentItems?)null);

            var result = await _sut.DeleteLentItemsArchiveAsync(Guid.NewGuid());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteLentItemsArchive_Returns_True_When_Deleted()
        {
            var archiveItem = MakeArchiveLentItem();

            _mockArchiveRepo.Setup(r => r.GetArchiveLentItemsByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockArchiveRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.DeleteLentItemsArchiveAsync(archiveItem.Id);

            result.Should().BeTrue();
            _mockArchiveRepo.Verify(r => r.DeleteArchiveLentItemsAsync(archiveItem.Id), Times.Once);
        }

        // ── RestoreLentItemsAsync ──────────────────────────────────────────────

        [Fact]
        public async Task RestoreLentItems_Returns_Null_When_ArchiveNotFound()
        {
            _mockArchiveRepo.Setup(r => r.GetArchiveLentItemsByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ArchiveLentItems?)null);

            var result = await _sut.RestoreLentItemsAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task RestoreLentItems_Returns_RestoredDto_On_Success()
        {
            var archiveItem = MakeArchiveLentItem();
            var lentItem    = new LentItems { Id = archiveItem.Id, ItemName = archiveItem.ItemName };
            var dto         = new ArchiveLentItemsDto { BorrowerFullName = "Juan" };

            _mockArchiveRepo.Setup(r => r.GetArchiveLentItemsByIdAsync(archiveItem.Id))
                            .ReturnsAsync(archiveItem);
            _mockMapper.Setup(m => m.Map<LentItems>(archiveItem)).Returns(lentItem);
            _mockMapper.Setup(m => m.Map<ArchiveLentItemsDto>(lentItem)).Returns(dto);
            _mockLentRepo.Setup(r => r.AddAsync(lentItem)).ReturnsAsync(lentItem);
            _mockArchiveRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.RestoreLentItemsAsync(archiveItem.Id);

            result.Should().NotBeNull();
            _mockLentRepo.Verify(r => r.AddAsync(lentItem), Times.Once);
            _mockArchiveRepo.Verify(r => r.DeleteArchiveLentItemsAsync(archiveItem.Id), Times.Once);
        }
    }
}
