using AutoMapper;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs;
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
    /// Tests for new AddBorrowAsync and AddReservationAsync methods
    /// </summary>
    public class AddBorrowAndReserveTests
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

        public AddBorrowAndReserveTests()
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
                .Setup(s => s.SendItemBorrowedNotificationAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

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

        // ── Helpers ──────────────────────────────────────────────────────────────
        private static Item MakeItem(
            ItemCondition condition = ItemCondition.Good,
            ItemStatus status = ItemStatus.Available) => new()
        {
            Id        = Guid.NewGuid(),
            ItemName  = "Projector",
            Condition = condition,
            Status    = status
        };

        private static User MakeUser(UserRole role = UserRole.Student) => new Student
        {
            Id        = Guid.NewGuid(),
            FirstName = "John",
            LastName  = "Doe",
            UserRole  = role
        };

        private static CreateBorrowDto ValidBorrowDto(Guid itemId, Guid? userId = null) => new()
        {
            ItemId              = itemId,
            UserId              = userId,
            Room                = "101",
            SubjectTimeSchedule = "MWF 8-9AM"
        };

        private static CreateReservationDto ValidReservationDto(Guid itemId, DateTime reservedFor, Guid? userId = null) => new()
        {
            ItemId              = itemId,
            UserId              = userId,
            Room                = "101",
            SubjectTimeSchedule = "MWF 8-9AM",
            ReservedFor         = reservedFor
        };

        // ════════════════════════════════════════════════════════════════════════
        // ADD BORROW ASYNC — instant RFID borrow, status = Borrowed
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AddBorrowAsync_Throws_When_ItemNotFound()
        {
            var itemId = Guid.NewGuid();
            _mockItemRepo.Setup(r => r.GetByIdAsync(itemId)).ReturnsAsync((Item?)null);

            Func<Task> act = () => _sut.AddBorrowAsync(ValidBorrowDto(itemId));

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Theory]
        [InlineData(ItemCondition.Defective)]
        [InlineData(ItemCondition.NeedRepair)]
        public async Task AddBorrowAsync_Throws_When_Item_IsInBadCondition(ItemCondition condition)
        {
            var item = MakeItem(condition);
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);

            Func<Task> act = () => _sut.AddBorrowAsync(ValidBorrowDto(item.Id));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{condition}*");
        }

        [Theory]
        [InlineData(ItemStatus.Borrowed)]
        [InlineData(ItemStatus.Reserved)]
        public async Task AddBorrowAsync_Throws_When_Item_IsAlreadyBorrowedOrReserved(ItemStatus status)
        {
            var item = MakeItem(status: status);
            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);

            Func<Task> act = () => _sut.AddBorrowAsync(ValidBorrowDto(item.Id));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{status.ToString().ToLower()}*");
        }

        [Fact]
        public async Task AddBorrowAsync_Throws_When_ActiveLentRecord_AlreadyExists()
        {
            var item = MakeItem();
            var activeLent = new LentItems { Id = Guid.NewGuid(), ItemId = item.Id, Status = "Borrowed" };

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LentItems> { activeLent });

            Func<Task> act = () => _sut.AddBorrowAsync(ValidBorrowDto(item.Id));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*active lent record*");
        }

        [Fact]
        public async Task AddBorrowAsync_Succeeds_And_Sets_Status_To_Borrowed()
        {
            var item = MakeItem();
            var user = MakeUser();
            var lent = new LentItems { Id = Guid.NewGuid(), ItemId = item.Id, Status = "Borrowed", LentAt = DateTime.UtcNow };
            var lentDto = new LentItemsDto();

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockUserService.Setup(s => s.ValidateStudentProfileComplete(user.Id)).ReturnsAsync((true, string.Empty));
            _mockUserRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateBorrowDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(lentDto);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.AddBorrowAsync(ValidBorrowDto(item.Id, user.Id));

            result.Should().NotBeNull();
            lent.Status.Should().Be("Borrowed");
            lent.LentAt.Should().NotBeNull();
            item.Status.Should().Be(ItemStatus.Borrowed);
        }

        [Fact]
        public async Task AddBorrowAsync_Sends_ItemBorrowed_Notification()
        {
            var item = MakeItem();
            var user = MakeUser();
            var lent = new LentItems { Id = Guid.NewGuid(), ItemId = item.Id, UserId = user.Id, ItemName = "Projector", BorrowerFullName = "John Doe", Status = "Borrowed" };

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockUserService.Setup(s => s.ValidateStudentProfileComplete(user.Id)).ReturnsAsync((true, string.Empty));
            _mockUserRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateBorrowDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(new LentItemsDto());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            await _sut.AddBorrowAsync(ValidBorrowDto(item.Id, user.Id));

            _mockNotification.Verify(s => s.SendItemBorrowedNotificationAsync(
                lent.Id, user.Id, "Projector", "John Doe"), Times.Once);
        }

        [Fact]
        public async Task AddBorrowAsync_WritesActivityLog_WithBorrowedCategory()
        {
            var item = MakeItem();
            var lent = new LentItems { Id = Guid.NewGuid(), ItemId = item.Id, Status = "Borrowed" };

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateBorrowDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(new LentItemsDto());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            await _sut.AddBorrowAsync(ValidBorrowDto(item.Id));

            _mockActivityLog.Verify(s => s.LogAsync(
                ActivityLogCategory.BorrowedItem,
                It.Is<string>(msg => msg.Contains("borrowed")),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>()), Times.Once);
        }

        // ════════════════════════════════════════════════════════════════════════
        // ADD RESERVATION ASYNC — future reservation, status = Pending
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AddReservationAsync_Throws_When_ReservedFor_IsInPast()
        {
            var item = MakeItem();
            var pastDate = DateTime.UtcNow.AddHours(-1);

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);

            Func<Task> act = () => _sut.AddReservationAsync(ValidReservationDto(item.Id, pastDate));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*future*");
        }

        [Fact]
        public async Task AddReservationAsync_Throws_When_ItemNotFound()
        {
            var itemId = Guid.NewGuid();
            var futureDate = DateTime.UtcNow.AddHours(2);

            _mockItemRepo.Setup(r => r.GetByIdAsync(itemId)).ReturnsAsync((Item?)null);

            Func<Task> act = () => _sut.AddReservationAsync(ValidReservationDto(itemId, futureDate));

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Theory]
        [InlineData(ItemCondition.Defective)]
        [InlineData(ItemCondition.NeedRepair)]
        public async Task AddReservationAsync_Throws_When_Item_IsInBadCondition(ItemCondition condition)
        {
            var item = MakeItem(condition);
            var futureDate = DateTime.UtcNow.AddHours(2);

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);

            Func<Task> act = () => _sut.AddReservationAsync(ValidReservationDto(item.Id, futureDate));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{condition}*");
        }

        [Fact]
        public async Task AddReservationAsync_Throws_When_Item_IsCurrentlyBorrowed()
        {
            var item = MakeItem(status: ItemStatus.Borrowed);
            var futureDate = DateTime.UtcNow.AddHours(2);

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);

            Func<Task> act = () => _sut.AddReservationAsync(ValidReservationDto(item.Id, futureDate));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*borrowed*");
        }

        [Fact]
        public async Task AddReservationAsync_Succeeds_And_Sets_Status_To_Pending()
        {
            var item = MakeItem();
            var user = MakeUser();
            var futureDate = DateTime.UtcNow.AddHours(2);
            var lent = new LentItems { Id = Guid.NewGuid(), ItemId = item.Id, Status = "Pending", ReservedFor = futureDate };
            var lentDto = new LentItemsDto();

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockUserService.Setup(s => s.ValidateStudentProfileComplete(user.Id)).ReturnsAsync((true, string.Empty));
            _mockUserRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateReservationDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(lentDto);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.AddReservationAsync(ValidReservationDto(item.Id, futureDate, user.Id));

            result.Should().NotBeNull();
            lent.Status.Should().Be("Pending");
            lent.LentAt.Should().BeNull();
            item.Status.Should().Be(ItemStatus.Reserved);
        }

        [Fact]
        public async Task AddReservationAsync_Sends_NewPendingRequest_Notification()
        {
            var item = MakeItem();
            var user = MakeUser();
            var futureDate = DateTime.UtcNow.AddHours(2);
            var lent = new LentItems { Id = Guid.NewGuid(), ItemId = item.Id, ItemName = "Projector", BorrowerFullName = "John Doe", Status = "Pending", ReservedFor = futureDate };

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockUserService.Setup(s => s.ValidateStudentProfileComplete(user.Id)).ReturnsAsync((true, string.Empty));
            _mockUserRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateReservationDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(new LentItemsDto());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            await _sut.AddReservationAsync(ValidReservationDto(item.Id, futureDate, user.Id));

            _mockNotification.Verify(s => s.SendNewPendingRequestNotificationAsync(
                lent.Id, "Projector", "John Doe", futureDate), Times.Once);
        }

        [Fact]
        public async Task AddReservationAsync_WritesActivityLog_WithGeneralCategory()
        {
            var item = MakeItem();
            var futureDate = DateTime.UtcNow.AddHours(2);
            var lent = new LentItems { Id = Guid.NewGuid(), ItemId = item.Id, Status = "Pending", ReservedFor = futureDate };

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockMapper.Setup(m => m.Map<LentItems>(It.IsAny<CreateReservationDto>())).Returns(lent);
            _mockMapper.Setup(m => m.Map<LentItemsDto>(It.IsAny<LentItems>())).Returns(new LentItemsDto());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<LentItems>())).ReturnsAsync(lent);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            await _sut.AddReservationAsync(ValidReservationDto(item.Id, futureDate));

            _mockActivityLog.Verify(s => s.LogAsync(
                ActivityLogCategory.General,
                It.Is<string>(msg => msg.Contains("Reservation")),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>()), Times.Once);
        }

        // ════════════════════════════════════════════════════════════════════════
        // BORROWING LIMIT — AddBorrowAsync
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AddBorrowAsync_Throws_When_BorrowingLimitReached_ForStudentOrTeacher()
        {
            // Arrange — student already has 3 active records
            var user = MakeUser(UserRole.Student);
            var item = MakeItem();

            var existingActive = Enumerable.Range(0, 3)
                .Select(_ => new LentItems { Id = Guid.NewGuid(), UserId = user.Id, Status = "Borrowed" })
                .ToList();

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(existingActive);
            _mockUserService.Setup(s => s.ValidateStudentProfileComplete(user.Id))
                            .ReturnsAsync((true, string.Empty));
            _mockUserRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            // Act
            Func<Task> act = () => _sut.AddBorrowAsync(ValidBorrowDto(item.Id, user.Id));

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Borrowing limit reached*");
        }

        // ════════════════════════════════════════════════════════════════════════
        // BORROWING LIMIT — AddReservationAsync
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AddReservationAsync_Throws_When_BorrowingLimitReached_ForStudentOrTeacher()
        {
            // Arrange — student already has 3 active records
            var user       = MakeUser(UserRole.Student);
            var item       = MakeItem();
            var futureDate = DateTime.UtcNow.AddHours(2);

            var existingActive = Enumerable.Range(0, 3)
                .Select(_ => new LentItems { Id = Guid.NewGuid(), UserId = user.Id, Status = "Borrowed" })
                .ToList();

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(existingActive);
            _mockUserService.Setup(s => s.ValidateStudentProfileComplete(user.Id))
                            .ReturnsAsync((true, string.Empty));
            _mockUserRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            // Act
            Func<Task> act = () => _sut.AddReservationAsync(ValidReservationDto(item.Id, futureDate, user.Id));

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Borrowing limit reached*");
        }

        // ════════════════════════════════════════════════════════════════════════
        // TIME SLOT CONFLICT — AddReservationAsync
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task AddReservationAsync_Throws_When_TimeSlotConflictsWithExistingReservation()
        {
            // The active-record guard fires before the slot check since both use the same
            // status set (Pending/Approved/Borrowed). A conflicting slot for the same item
            // always triggers the active-record guard first.
            var item       = MakeItem();
            var futureDate = DateTime.UtcNow.AddHours(4);

            var conflicting = new LentItems
            {
                Id          = Guid.NewGuid(),
                ItemId      = item.Id,
                Status      = "Pending",
                ReservedFor = futureDate.AddMinutes(30) // within 2-hour window
            };

            _mockItemRepo.Setup(r => r.GetByIdAsync(item.Id)).ReturnsAsync(item);
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LentItems> { conflicting });

            // Act
            Func<Task> act = () => _sut.AddReservationAsync(ValidReservationDto(item.Id, futureDate));

            // Assert — active-record guard fires first
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*active lent record*");
        }
    }
}
