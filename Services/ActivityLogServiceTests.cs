using AutoMapper;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs.ActivityLog;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 5 — Activity Logs (ActivityLogService)
    /// Covers: GetAll (with filters), GetById, GetBorrowLogs, LogAsync
    /// All dependencies mocked — zero DB latency.
    /// </summary>
    public class ActivityLogServiceTests
    {
        private readonly Mock<IActivityLogRepository> _mockRepo;
        private readonly Mock<IMapper>                _mockMapper;

        private readonly ActivityLogService _sut;

        public ActivityLogServiceTests()
        {
            _mockRepo   = new Mock<IActivityLogRepository>();
            _mockMapper = new Mock<IMapper>();

            _sut = new ActivityLogService(_mockRepo.Object, _mockMapper.Object);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ActivityLog MakeLog(
            ActivityLogCategory category = ActivityLogCategory.BorrowedItem,
            Guid? actorId = null,
            Guid? itemId  = null) => new()
        {
            Id          = Guid.NewGuid(),
            Category    = category,
            Action      = "Test action",
            ActorUserId = actorId ?? Guid.NewGuid(),
            ItemId      = itemId  ?? Guid.NewGuid(),
            ActorName   = "Juan Dela Cruz",
            ActorRole   = "Student",
            ItemName    = "Projector",
            CreatedAt   = DateTime.UtcNow
        };

        private static ActivityLogDto ToDto(ActivityLog log) => new()
        {
            Id       = log.Id,
            Category = log.Category,
            Action   = log.Action
        };

        // ══════════════════════════════════════════════════════════════════════
        // GET ALL (filtered)
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAll_Returns_AllLogs_When_NoFilterApplied()
        {
            var logs = new List<ActivityLog> { MakeLog(), MakeLog() };
            var dtos = logs.Select(ToDto).ToList();

            _mockRepo.Setup(r => r.GetFilteredAsync(null, null, null, null, null, null))
                     .ReturnsAsync(logs);
            _mockMapper.Setup(m => m.Map<IEnumerable<ActivityLogDto>>(logs)).Returns(dtos);

            var result = await _sut.GetAllAsync(new ActivityLogFilterDto());

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetAll_PassesCategory_Filter_To_Repository()
        {
            var filter = new ActivityLogFilterDto { Category = ActivityLogCategory.Returned };
            var logs   = new List<ActivityLog> { MakeLog(ActivityLogCategory.Returned) };
            var dtos   = logs.Select(ToDto).ToList();

            _mockRepo.Setup(r => r.GetFilteredAsync(
                ActivityLogCategory.Returned, null, null, null, null, null))
                     .ReturnsAsync(logs);
            _mockMapper.Setup(m => m.Map<IEnumerable<ActivityLogDto>>(logs)).Returns(dtos);

            var result = await _sut.GetAllAsync(filter);

            result.Should().ContainSingle();
            _mockRepo.Verify(r => r.GetFilteredAsync(
                ActivityLogCategory.Returned, null, null, null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetAll_PassesDateRange_Filter_To_Repository()
        {
            var from   = DateTime.UtcNow.AddDays(-7);
            var to     = DateTime.UtcNow;
            var filter = new ActivityLogFilterDto { From = from, To = to };
            var logs   = new List<ActivityLog> { MakeLog() };
            var dtos   = logs.Select(ToDto).ToList();

            _mockRepo.Setup(r => r.GetFilteredAsync(null, from, to, null, null, null))
                     .ReturnsAsync(logs);
            _mockMapper.Setup(m => m.Map<IEnumerable<ActivityLogDto>>(logs)).Returns(dtos);

            var result = await _sut.GetAllAsync(filter);

            result.Should().ContainSingle();
        }

        [Fact]
        public async Task GetAll_PassesActorUserId_Filter_To_Repository()
        {
            var userId = Guid.NewGuid();
            var filter = new ActivityLogFilterDto { ActorUserId = userId };
            var logs   = new List<ActivityLog> { MakeLog(actorId: userId) };
            var dtos   = logs.Select(ToDto).ToList();

            _mockRepo.Setup(r => r.GetFilteredAsync(null, null, null, userId, null, null))
                     .ReturnsAsync(logs);
            _mockMapper.Setup(m => m.Map<IEnumerable<ActivityLogDto>>(logs)).Returns(dtos);

            var result = await _sut.GetAllAsync(filter);

            result.Should().ContainSingle();
        }

        [Fact]
        public async Task GetAll_PassesItemId_Filter_To_Repository()
        {
            var itemId = Guid.NewGuid();
            var filter = new ActivityLogFilterDto { ItemId = itemId };
            var logs   = new List<ActivityLog> { MakeLog(itemId: itemId) };
            var dtos   = logs.Select(ToDto).ToList();

            _mockRepo.Setup(r => r.GetFilteredAsync(null, null, null, null, itemId, null))
                     .ReturnsAsync(logs);
            _mockMapper.Setup(m => m.Map<IEnumerable<ActivityLogDto>>(logs)).Returns(dtos);

            var result = await _sut.GetAllAsync(filter);

            result.Should().ContainSingle();
        }

        [Fact]
        public async Task GetAll_PassesStatus_Filter_To_Repository()
        {
            var filter = new ActivityLogFilterDto { Status = "Borrowed" };
            var logs   = new List<ActivityLog> { MakeLog() };
            var dtos   = logs.Select(ToDto).ToList();

            _mockRepo.Setup(r => r.GetFilteredAsync(null, null, null, null, null, "Borrowed"))
                     .ReturnsAsync(logs);
            _mockMapper.Setup(m => m.Map<IEnumerable<ActivityLogDto>>(logs)).Returns(dtos);

            var result = await _sut.GetAllAsync(filter);

            result.Should().ContainSingle();
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET BY ID
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetById_Returns_Null_When_NotFound()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                     .ReturnsAsync((ActivityLog?)null);

            var result = await _sut.GetByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetById_Returns_MappedDto_When_Found()
        {
            var log = MakeLog();
            var dto = ToDto(log);

            _mockRepo.Setup(r => r.GetByIdAsync(log.Id)).ReturnsAsync(log);
            _mockMapper.Setup(m => m.Map<ActivityLogDto>(log)).Returns(dto);

            var result = await _sut.GetByIdAsync(log.Id);

            result.Should().BeEquivalentTo(dto);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET BORROW LOGS
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetBorrowLogs_Queries_BorrowedItem_And_Returned_Categories()
        {
            var borrowedLogs  = new List<ActivityLog> { MakeLog(ActivityLogCategory.BorrowedItem) };
            var returnedLogs  = new List<ActivityLog> { MakeLog(ActivityLogCategory.Returned) };
            var combinedDtos  = new List<BorrowLogDto> { new(), new() };

            _mockRepo.Setup(r => r.GetFilteredAsync(
                ActivityLogCategory.BorrowedItem, null, null, null, null, null))
                     .ReturnsAsync(borrowedLogs);
            _mockRepo.Setup(r => r.GetFilteredAsync(
                ActivityLogCategory.Returned, null, null, null, null, null))
                     .ReturnsAsync(returnedLogs);
            _mockMapper.Setup(m => m.Map<IEnumerable<BorrowLogDto>>(It.IsAny<IEnumerable<ActivityLog>>()))
                       .Returns(combinedDtos);

            var result = await _sut.GetBorrowLogsAsync(null, null, null, null);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetBorrowLogs_Returns_Empty_When_NoLogsExist()
        {
            _mockRepo.Setup(r => r.GetFilteredAsync(
                ActivityLogCategory.BorrowedItem, null, null, null, null, null))
                     .ReturnsAsync(Enumerable.Empty<ActivityLog>());
            _mockRepo.Setup(r => r.GetFilteredAsync(
                ActivityLogCategory.Returned, null, null, null, null, null))
                     .ReturnsAsync(Enumerable.Empty<ActivityLog>());
            _mockMapper.Setup(m => m.Map<IEnumerable<BorrowLogDto>>(It.IsAny<IEnumerable<ActivityLog>>()))
                       .Returns(Enumerable.Empty<BorrowLogDto>());

            var result = await _sut.GetBorrowLogsAsync(null, null, null, null);

            result.Should().BeEmpty();
        }

        // ══════════════════════════════════════════════════════════════════════
        // LOG ASYNC
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task LogAsync_Saves_Log_And_Returns_MappedDto()
        {
            var dto = new ActivityLogDto { Action = "Borrow request created" };

            _mockRepo.Setup(r => r.AddAsync(It.IsAny<ActivityLog>()))
                     .ReturnsAsync(new ActivityLog());
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<ActivityLogDto>(It.IsAny<ActivityLog>())).Returns(dto);

            var result = await _sut.LogAsync(
                ActivityLogCategory.BorrowedItem,
                "Borrow request created",
                Guid.NewGuid(), "Juan", "Student",
                Guid.NewGuid(), "Projector", "SN-001",
                Guid.NewGuid(), null, "Borrowed",
                DateTime.UtcNow, null, null, null);

            result.Should().BeEquivalentTo(dto);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<ActivityLog>()), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task LogAsync_Stores_Correct_Category_And_Action()
        {
            ActivityLog? captured = null;
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<ActivityLog>()))
                     .Callback<ActivityLog>(l => captured = l)
                     .ReturnsAsync(new ActivityLog());
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<ActivityLogDto>(It.IsAny<ActivityLog>()))
                       .Returns(new ActivityLogDto());

            await _sut.LogAsync(
                ActivityLogCategory.Returned,
                "Item returned",
                null, "Staff", "Staff",
                null, "Projector", null,
                null, "Borrowed", "Returned",
                null, DateTime.UtcNow, null, "On time");

            captured.Should().NotBeNull();
            captured!.Category.Should().Be(ActivityLogCategory.Returned);
            captured.Action.Should().Be("Item returned");
            captured.NewStatus.Should().Be("Returned");
            captured.PreviousStatus.Should().Be("Borrowed");
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET BORROW LOGS — date/user/item filter
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetBorrowLogs_FiltersBy_DateRange_UserId_ItemId()
        {
            // Arrange
            var from   = DateTime.UtcNow.AddDays(-7);
            var to     = DateTime.UtcNow;
            var userId = Guid.NewGuid();
            var itemId = Guid.NewGuid();

            var borrowedLogs = new List<ActivityLog> { MakeLog(ActivityLogCategory.BorrowedItem, actorId: userId, itemId: itemId) };
            var returnedLogs = new List<ActivityLog> { MakeLog(ActivityLogCategory.Returned,     actorId: userId, itemId: itemId) };

            _mockRepo.Setup(r => r.GetFilteredAsync(
                ActivityLogCategory.BorrowedItem, from, to, userId, itemId, null))
                     .ReturnsAsync(borrowedLogs);
            _mockRepo.Setup(r => r.GetFilteredAsync(
                ActivityLogCategory.Returned, from, to, userId, itemId, null))
                     .ReturnsAsync(returnedLogs);
            _mockMapper.Setup(m => m.Map<IEnumerable<BorrowLogDto>>(It.IsAny<IEnumerable<ActivityLog>>()))
                       .Returns(new List<BorrowLogDto> { new(), new() });

            // Act
            var result = await _sut.GetBorrowLogsAsync(from, to, userId, itemId);

            // Assert
            result.Should().HaveCount(2);
            _mockRepo.Verify(r => r.GetFilteredAsync(
                ActivityLogCategory.BorrowedItem, from, to, userId, itemId, null), Times.Once);
            _mockRepo.Verify(r => r.GetFilteredAsync(
                ActivityLogCategory.Returned, from, to, userId, itemId, null), Times.Once);
        }
    }
}
