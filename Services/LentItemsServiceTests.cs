using AutoMapper;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs;
using BackendTechnicalAssetsManagement.src.DTOs.Archive.LentItems;
using BackendTechnicalAssetsManagement.src.DTOs.LentItems;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 4 — Lending & Circulation (LentItemsService)
    /// Covers: AddAsync, AddForGuestAsync, GetAll/ById, UpdateHistoryVisibility,
    ///         ArchiveLentItems, CancelExpiredReservations
    /// All dependencies mocked — zero DB latency.
    /// </summary>
    public class LentItemsServiceTests
    {
        private readonly Mock<ILentItemsRepository>     _mockRepo;
        private readonly Mock<IMapper>                  _mockMapper;
        private readonly Mock<IUserRepository>          _mockUserRepo;
        private readonly Mock<IItemRepository>          _mockItemRepo;
        private readonly Mock<IArchiveLentItemsService> _mockArchive;
        private readonly Mock<IUserService>             _mockUserService;
        private readonly Mock<INotificationService>     _mockNotification;
        private readonly Mock<IActivityLogService>      _mockActivityLog;
        private readonly Mock<ISupabaseStorageService>  _mockStorage;

        private readonly LentItemsService _sut;

        public LentItemsServiceTests()
        {
            _mockRepo         = new Mock<ILentItemsRepository>();
            _mockMapper       = new Mock<IMapper>();
            _mockUserRepo     = new Mock<IUserRepository>();
            _mockItemRepo     = new Mock<IItemRepository>();
            _mockArchive      = new Mock<IArchiveLentItemsService>();
            _mockUserService  = new Mock<IUserService>();
            _mockNotification = new Mock<INotificationService>();
            _mockActivityLog  = new Mock<IActivityLogService>();
            _mockStorage      = new Mock<ISupabaseStorageService>();

            // Default: LogAsync returns a dummy DTO so WriteLogAsync never throws
            _mockActivityLog
                .Setup(s => s.LogAsync(
                    It.IsAny<ActivityLogCategory>(), It.IsAny<string>(),
                    It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(new BackendTechnicalAssetsManagement.src.DTOs.ActivityLog.ActivityLogDto());

            _mockNotification
                .Setup(s => s.SendNewPendingRequestNotificationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
                .Returns(Task.CompletedTask);

            _sut = new LentItemsService(
                _mockRepo.Object,
                _mockMapper.Object,
                _mockUserRepo.Object,
                _mockItemRepo.Object,
                _mockArchive.Object,
                _mockUserService.Object,
                _mockNotification.Object,
                _mockActivityLog.Object,
                _mockStorage.Object
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Item MakeItem(
            ItemCondition condition = ItemCondition.Good,
            ItemStatus status = ItemStatus.Available,
            string? rfid = null) => new()
        {
            Id        = Guid.NewGuid(),
            ItemName  = "Projector",
            Condition = condition,
            Status    = status,
            RfidUid   = rfid
        };

        private static LentItems MakeLentRecord(
            Guid? itemId = null,
            Guid? userId = null,
            string status = "Borrowed") => new()
        {
            Id       = Guid.NewGuid(),
            ItemId   = itemId ?? Guid.NewGuid(),
            UserId   = userId,
            ItemName = "Projector",
            Status   = status
        };

        private static CreateLentItemDto ValidBorrowDto(Guid itemId, Guid? userId = null) => new()
        {
            ItemId              = itemId,
            UserId              = userId,
            Room                = "101",
            SubjectTimeSchedule = "MWF 8-9AM",
            Status              = "Borrowed"
        };

        private static CreateLentItemsForGuestDto ValidGuestDto(string tagUid = "AABBCCDD") => new()
        {
            TagUid              = tagUid,
            BorrowerFirstName   = "Guest",
            BorrowerLastName    = "User",
            Room                = "101",
            SubjectTimeSchedule = "MWF 8-9AM",
            Status              = "Borrowed"
        };

        // ══════════════════════════════════════════════════════════════════════
        // ADD ASYNC — item condition guards
        // ══════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData(ItemCondition.Defective)]
        [InlineData(ItemCondition.NeedRepair)]
        public async Task AddAsync_Throws_When_Item_IsInBadCondition(ItemCondition condition)
        {
            var item = MakeItem(condition);
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);

            Func<Task> act = () => _sut.AddAsync(ValidBorrowDto(item.Id));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{condition}*");
        }

        [Theory]
        [InlineData(ItemStatus.Borrowed)]
        [InlineData(ItemStatus.Reserved)]
        public async Task AddAsync_Throws_When_Item_IsAlreadyBorrowedOrReserved(ItemStatus status)
        {
            var item = MakeItem(status: status);
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);

            Func<Task> act = () => _sut.AddAsync(ValidBorrowDto(item.Id));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{status.ToString().ToLower()}*");
        }

        [Fact]
        public async Task AddAsync_Throws_When_ActiveLentRecord_AlreadyExists()
        {
            var item       = MakeItem();
            var activeLent = MakeLentRecord(itemId: item.Id, status: "Borrowed");

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LentItems> { activeLent });

            Func<Task> act = () => _sut.AddAsync(ValidBorrowDto(item.Id));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*active lent record*");
        }

        [Fact]
        public async Task AddAsync_Throws_When_ItemNotFound()
        {
            var itemId = Guid.NewGuid();
            _mockItemRepo.Setup(r => r.GetByIdAsync(itemId)).ReturnsAsync((Item?)null);

            Func<Task> act = () => _sut.AddAsync(ValidBorrowDto(itemId));

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task AddAsync_Succeeds_And_Returns_MappedDto()
        {
            var item    = MakeItem();
            var lent    = MakeLentRecord(itemId: item.Id, status: "Borrowed");
            var lentDto = new LentItemsDto();

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateLentItemDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(lentDto);

            var result = await _sut.AddAsync(ValidBorrowDto(item.Id));

            result.Should().NotBeNull();
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<LentItems>()), Times.Once);
        }

        [Fact]
        public async Task AddAsync_WritesActivityLog_OnSuccess()
        {
            var item = MakeItem();
            var lent = MakeLentRecord(itemId: item.Id, status: "Borrowed");

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateLentItemDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(new LentItemsDto());

            await _sut.AddAsync(ValidBorrowDto(item.Id));

            _mockActivityLog.Verify(s => s.LogAsync(
                It.IsAny<ActivityLogCategory>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>()), Times.AtLeastOnce);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ADD FOR GUEST ASYNC
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AddForGuestAsync_Throws_When_TagUidNotFound()
        {
            _mockItemRepo.Setup(r => r.GetByRfidUidAsync(It.IsAny<string>()))
                         .ReturnsAsync((Item?)null);
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                         .ReturnsAsync((User?)null);

            Func<Task> act = () => _sut.AddForGuestAsync(ValidGuestDto("UNKNOWN"), Guid.NewGuid());

            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("*UNKNOWN*");
        }

        [Theory]
        [InlineData(ItemCondition.Defective)]
        [InlineData(ItemCondition.NeedRepair)]
        public async Task AddForGuestAsync_Throws_When_Item_IsInBadCondition(ItemCondition condition)
        {
            var item = MakeItem(condition);
            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync(item);
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            Func<Task> act = () => _sut.AddForGuestAsync(ValidGuestDto(), Guid.NewGuid());

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{condition}*");
        }

        [Fact]
        public async Task AddForGuestAsync_Throws_When_Item_IsAlreadyBorrowed()
        {
            var item = MakeItem(status: ItemStatus.Borrowed);
            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync(item);
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            Func<Task> act = () => _sut.AddForGuestAsync(ValidGuestDto(), Guid.NewGuid());

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*borrowed*");
        }

        [Fact]
        public async Task AddForGuestAsync_Sets_BorrowerRole_To_Guest()
        {
            var item     = MakeItem();
            var issuerId = Guid.NewGuid();
            LentItems? captured = null;

            _mockItemRepo.Setup(r => r.GetByRfidUidAsync("AABBCCDD")).ReturnsAsync(item);
            _mockUserRepo.Setup(r => r.GetByIdAsync(issuerId)).ReturnsAsync((User?)null);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>()))
                     .Callback<LentItems>(l => captured = l)
                     .ReturnsAsync(new LentItems());
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new LentItems());
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateLentItemsForGuestDto>()))
                       .Returns(new LentItems { Status = "Borrowed" });
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>()))
                       .Returns(new LentItemsDto());

            await _sut.AddForGuestAsync(ValidGuestDto(), issuerId);

            captured.Should().NotBeNull();
            captured!.BorrowerRole.Should().Be("Guest");
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET ALL / GET BY ID
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAll_Returns_AllLentItems()
        {
            var records = new List<LentItems> { MakeLentRecord(), MakeLentRecord() };
            var dtos    = records.Select(_ => new LentItemsDto()).ToList();

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            _mockMapper.Setup(m => m.Map<IEnumerable<LentItemsDto>>(records)).Returns(dtos);

            var result = await _sut.GetAllAsync();

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetById_Returns_Null_When_NotFound()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((LentItems?)null);
            _mockMapper.Setup(m => m.Map<LentItemsDto?>(null)).Returns((LentItemsDto?)null);

            var result = await _sut.GetByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetById_Returns_MappedDto_When_Found()
        {
            var record = MakeLentRecord();
            var dto    = new LentItemsDto();

            _mockRepo.Setup(r => r.GetByIdAsync(record.Id)).ReturnsAsync(record);
            _mockMapper.Setup(m => m.Map<LentItemsDto?>(record)).Returns(dto);

            var result = await _sut.GetByIdAsync(record.Id);

            result.Should().NotBeNull();
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE HISTORY VISIBILITY
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateHistoryVisibility_Returns_False_When_LentItemNotFound()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((LentItems?)null);

            var result = await _sut.UpdateHistoryVisibility(Guid.NewGuid(), Guid.NewGuid(), true);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateHistoryVisibility_Returns_False_When_UserDoesNotOwnRecord()
        {
            var record          = MakeLentRecord(userId: Guid.NewGuid());
            var differentUserId = Guid.NewGuid();

            _mockRepo.Setup(r => r.GetByIdAsync(record.Id)).ReturnsAsync(record);

            var result = await _sut.UpdateHistoryVisibility(record.Id, differentUserId, true);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateHistoryVisibility_Returns_True_When_UserOwnsRecord()
        {
            var userId = Guid.NewGuid();
            var record = MakeLentRecord(userId: userId);
            record.IsHiddenFromUser = false;

            _mockRepo.Setup(r => r.GetByIdAsync(record.Id)).ReturnsAsync(record);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.UpdateHistoryVisibility(record.Id, userId, true);

            result.Should().BeTrue();
            record.IsHiddenFromUser.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateHistoryVisibility_Returns_True_Without_Save_When_AlreadyInDesiredState()
        {
            var userId = Guid.NewGuid();
            var record = MakeLentRecord(userId: userId);
            record.IsHiddenFromUser = true; // already hidden

            _mockRepo.Setup(r => r.GetByIdAsync(record.Id)).ReturnsAsync(record);

            var result = await _sut.UpdateHistoryVisibility(record.Id, userId, true);

            result.Should().BeTrue();
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ARCHIVE LENT ITEMS
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ArchiveLentItems_Returns_Failure_When_LentItemNotFound()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((LentItems?)null);

            var (success, error) = await _sut.ArchiveLentItems(Guid.NewGuid());

            success.Should().BeFalse();
            error.Should().Be("Lent item not found.");
        }

        [Fact]
        public async Task ArchiveLentItems_Returns_Success_When_Archived()
        {
            var item   = MakeItem();
            var record = MakeLentRecord(itemId: item.Id, status: "Returned");

            _mockRepo.Setup(r => r.GetByIdAsync(record.Id)).ReturnsAsync(record);
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockArchive.Setup(s => s.CreateLentItemsArchiveAsync(It.IsAny<CreateArchiveLentItemsDto>()))
                        .ReturnsAsync(new ArchiveLentItemsDto());
            _mockMapper.Setup(m => m.Map<CreateArchiveLentItemsDto>(record))
                       .Returns(new CreateArchiveLentItemsDto());
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var (success, error) = await _sut.ArchiveLentItems(record.Id);

            success.Should().BeTrue();
            error.Should().BeEmpty();
        }

        [Fact]
        public async Task ArchiveLentItems_Sets_Item_To_Available_When_Returned()
        {
            var item   = MakeItem(status: ItemStatus.Borrowed);
            var record = MakeLentRecord(itemId: item.Id, status: "Returned");

            _mockRepo.Setup(r => r.GetByIdAsync(record.Id)).ReturnsAsync(record);
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockArchive.Setup(s => s.CreateLentItemsArchiveAsync(It.IsAny<CreateArchiveLentItemsDto>()))
                        .ReturnsAsync(new ArchiveLentItemsDto());
            _mockMapper.Setup(m => m.Map<CreateArchiveLentItemsDto>(record))
                       .Returns(new CreateArchiveLentItemsDto());
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            await _sut.ArchiveLentItems(record.Id);

            item.Status.Should().Be(ItemStatus.Available);
        }

        // ══════════════════════════════════════════════════════════════════════
        // CANCEL EXPIRED RESERVATIONS
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CancelExpiredReservations_Returns_Zero_When_NoExpiredReservations()
        {
            var records = new List<LentItems>
            {
                new() { Id = Guid.NewGuid(), Status = "Pending",
                        ReservedFor = DateTime.UtcNow.AddDays(1), ItemId = Guid.NewGuid() }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);

            var count = await _sut.CancelExpiredReservationsAsync();

            count.Should().Be(0);
        }

        [Fact]
        public async Task CancelExpiredReservations_Cancels_Stale_Reservations_And_Returns_Count()
        {
            var item  = MakeItem();
            var stale = new LentItems
            {
                Id          = Guid.NewGuid(),
                ItemId      = item.Id,
                Status      = "Pending",
                ReservedFor = DateTime.UtcNow.AddHours(-3), // past grace period
                LentAt      = null
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LentItems> { stale });
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var count = await _sut.CancelExpiredReservationsAsync();

            count.Should().Be(1);
            stale.Status.Should().Be("Canceled");
            item.Status.Should().Be(ItemStatus.Available);
        }

        [Fact]
        public async Task CancelExpiredReservations_DoesNotCancel_AlreadyPickedUp_Reservations()
        {
            var record = new LentItems
            {
                Id          = Guid.NewGuid(),
                ItemId      = Guid.NewGuid(),
                Status      = "Borrowed",
                ReservedFor = DateTime.UtcNow.AddHours(-3),
                LentAt      = DateTime.UtcNow.AddHours(-2) // already picked up
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LentItems> { record });

            var count = await _sut.CancelExpiredReservationsAsync();

            count.Should().Be(0);
        }
    }
}
