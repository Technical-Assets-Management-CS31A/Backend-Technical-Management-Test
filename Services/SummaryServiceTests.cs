using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 6 — Statistical Summaries (SummaryService)
    /// Covers: GetItemCount, GetLentItemsCount, GetActiveUserCount, GetOverallSummary
    /// All dependencies mocked — zero DB latency.
    /// </summary>
    public class SummaryServiceTests
    {
        private readonly Mock<IItemRepository>       _mockItemRepo;
        private readonly Mock<ILentItemsRepository>  _mockLentRepo;
        private readonly Mock<IUserRepository>       _mockUserRepo;

        private readonly SummaryService _sut;

        public SummaryServiceTests()
        {
            _mockItemRepo = new Mock<IItemRepository>();
            _mockLentRepo = new Mock<ILentItemsRepository>();
            _mockUserRepo = new Mock<IUserRepository>();

            _sut = new SummaryService(
                _mockItemRepo.Object,
                _mockLentRepo.Object,
                _mockUserRepo.Object
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Item MakeItem(ItemCondition condition, ItemStatus status = ItemStatus.Available,
            ItemCategory category = ItemCategory.Electronics, string type = "Projector") => new()
        {
            Id        = Guid.NewGuid(),
            ItemName  = "Item",
            ItemType  = type,
            Condition = condition,
            Status    = status,
            Category  = category
        };

        private static LentItems MakeLentRecord(bool returned = false) => new()
        {
            Id         = Guid.NewGuid(),
            ReturnedAt = returned ? DateTime.UtcNow : null
        };

        private static User MakeUser(UserRole role, string status = "Online") => new()
        {
            Id       = Guid.NewGuid(),
            UserRole = role,
            Status   = status
        };

        // ══════════════════════════════════════════════════════════════════════
        // GET ITEM COUNT
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetItemCount_Calculates_Correct_Condition_Counts()
        {
            var items = new List<Item>
            {
                MakeItem(ItemCondition.New),
                MakeItem(ItemCondition.Good),
                MakeItem(ItemCondition.Good),
                MakeItem(ItemCondition.Defective),
                MakeItem(ItemCondition.NeedRepair),
                MakeItem(ItemCondition.Refurbished)
            };
            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(items);

            var result = await _sut.GetItemCountAsync();

            result.TotalItems.Should().Be(6);
            result.NewItems.Should().Be(1);
            result.GoodItems.Should().Be(2);
            result.DefectiveItems.Should().Be(1);
            result.NeedRepairItems.Should().Be(1);
            result.RefurbishedItems.Should().Be(1);
        }

        [Fact]
        public async Task GetItemCount_Calculates_Correct_Category_Counts()
        {
            var items = new List<Item>
            {
                MakeItem(ItemCondition.Good, category: ItemCategory.Electronics),
                MakeItem(ItemCondition.Good, category: ItemCategory.Electronics),
                MakeItem(ItemCondition.Good, category: ItemCategory.Keys),
                MakeItem(ItemCondition.Good, category: ItemCategory.MediaEquipment),
                MakeItem(ItemCondition.Good, category: ItemCategory.Tools),
                MakeItem(ItemCondition.Good, category: ItemCategory.Miscellaneous)
            };
            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(items);

            var result = await _sut.GetItemCountAsync();

            result.Electronic.Should().Be(2);
            result.Keys.Should().Be(1);
            result.MediaEquipment.Should().Be(1);
            result.Tools.Should().Be(1);
            result.Miscellaneous.Should().Be(1);
        }

        [Fact]
        public async Task GetItemCount_Returns_Zeros_When_NoItems()
        {
            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<Item>());

            var result = await _sut.GetItemCountAsync();

            result.TotalItems.Should().Be(0);
            result.DefectiveItems.Should().Be(0);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET LENT ITEMS COUNT
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetLentItemsCount_Splits_Active_Vs_Returned_Correctly()
        {
            var records = new List<LentItems>
            {
                MakeLentRecord(returned: false),
                MakeLentRecord(returned: false),
                MakeLentRecord(returned: true)
            };
            _mockLentRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);

            var result = await _sut.GetLentItemsCountAsync();

            result.TotalLentItems.Should().Be(3);
            result.CurrentlyLentItems.Should().Be(2);
            result.ReturnedLentItems.Should().Be(1);
        }

        [Fact]
        public async Task GetLentItemsCount_Returns_Zeros_When_NoRecords()
        {
            _mockLentRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());

            var result = await _sut.GetLentItemsCountAsync();

            result.TotalLentItems.Should().Be(0);
            result.CurrentlyLentItems.Should().Be(0);
            result.ReturnedLentItems.Should().Be(0);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET ACTIVE USER COUNT
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetActiveUserCount_Separates_Roles_Into_Correct_Buckets()
        {
            var users = new List<User>
            {
                MakeUser(UserRole.SuperAdmin, "Online"),
                MakeUser(UserRole.Admin,      "Online"),
                MakeUser(UserRole.Staff,      "Online"),
                MakeUser(UserRole.Staff,      "Online"),
                MakeUser(UserRole.Teacher,    "Online"),
                MakeUser(UserRole.Student,    "Online"),
                MakeUser(UserRole.Student,    "Online"),
                MakeUser(UserRole.Student,    "Offline") // should NOT be counted
            };
            _mockUserRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

            var result = await _sut.GetActiveUserCountAsync();

            result.TotalActiveUsers.Should().Be(7);
            result.TotalActiveAdmins.Should().Be(2);   // SuperAdmin + Admin
            result.TotalActiveStaffs.Should().Be(2);
            result.TotalActiveTeachers.Should().Be(1);
            result.TotalActiveStudents.Should().Be(2); // offline student excluded
        }

        [Fact]
        public async Task GetActiveUserCount_Returns_Zeros_When_AllUsersOffline()
        {
            var users = new List<User>
            {
                MakeUser(UserRole.Student, "Offline"),
                MakeUser(UserRole.Teacher, "Offline")
            };
            _mockUserRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

            var result = await _sut.GetActiveUserCountAsync();

            result.TotalActiveUsers.Should().Be(0);
            result.TotalActiveStudents.Should().Be(0);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET OVERALL SUMMARY
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetOverallSummary_Aggregates_All_Sub_Summaries()
        {
            var items = new List<Item>
            {
                MakeItem(ItemCondition.Good, ItemStatus.Available, type: "Projector"),
                MakeItem(ItemCondition.Good, ItemStatus.Borrowed,  type: "Projector"),
                MakeItem(ItemCondition.Good, ItemStatus.Available, type: "Cable")
            };
            var lentRecords = new List<LentItems> { MakeLentRecord(), MakeLentRecord() };
            var users       = new List<User> { MakeUser(UserRole.Student, "Online") };

            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(items);
            _mockLentRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(lentRecords);
            _mockUserRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

            var result = await _sut.GetOverallSummaryAsync();

            result.TotalItems.Should().Be(3);
            result.TotalLentItems.Should().Be(2);
            result.TotalActiveUsers.Should().Be(1);
            result.ItemStocks.Should().NotBeNull();
            result.ItemStocks!.Should().HaveCount(2); // Projector + Cable
        }

        [Fact]
        public async Task GetOverallSummary_ItemStocks_Counts_Available_And_Borrowed_Correctly()
        {
            var items = new List<Item>
            {
                MakeItem(ItemCondition.Good, ItemStatus.Available, type: "Projector"),
                MakeItem(ItemCondition.Good, ItemStatus.Available, type: "Projector"),
                MakeItem(ItemCondition.Good, ItemStatus.Borrowed,  type: "Projector")
            };

            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(items);
            _mockLentRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockUserRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<User>());

            var result = await _sut.GetOverallSummaryAsync();

            var projectorStock = result.ItemStocks!.First(s => s.ItemType == "Projector");
            projectorStock.TotalCount.Should().Be(3);
            projectorStock.AvailableCount.Should().Be(2);
            projectorStock.BorrowedCount.Should().Be(1);
        }
    }
}
