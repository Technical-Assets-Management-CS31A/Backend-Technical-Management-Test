using AutoMapper;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs.User;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Models.DTOs.Users;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;
using static BackendTechnicalAssetsManagement.src.DTOs.User.UserProfileDtos;
using Enums = BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 2 — User Management
    /// Covers: GetUserProfileById, GetAllUsers, GetUserById,
    ///         UpdateUserProfile, UpdateStudentProfile, UpdateStaffOrAdminProfile,
    ///         DeleteUser, ValidateStudentProfileComplete,
    ///         GetStudentByIdNumber, RegisterRfid, GetStudentByRfid
    /// All dependencies are mocked — zero DB latency.
    /// </summary>
    public class UserServiceTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IUserRepository>         _mockUserRepo;
        private readonly Mock<IMapper>                 _mockMapper;
        private readonly Mock<IArchiveUserService>     _mockArchiveUserService;
        private readonly Mock<IPasswordHashingService> _mockPasswordHashing;
        private readonly Mock<IExcelReaderService>     _mockExcelReader;
        private readonly Mock<ISupabaseStorageService> _mockStorage;
        private readonly Mock<IItemRepository>         _mockItemRepo;
        private readonly Mock<ILentItemsRepository>    _mockLentRepo;

        private readonly UserService _sut;

        public UserServiceTests()
        {
            _mockUserRepo           = new Mock<IUserRepository>();
            _mockMapper             = new Mock<IMapper>();
            _mockArchiveUserService = new Mock<IArchiveUserService>();
            _mockPasswordHashing    = new Mock<IPasswordHashingService>();
            _mockExcelReader        = new Mock<IExcelReaderService>();
            _mockStorage            = new Mock<ISupabaseStorageService>();
            _mockItemRepo           = new Mock<IItemRepository>();
            _mockLentRepo           = new Mock<ILentItemsRepository>();

            _sut = new UserService(
                _mockUserRepo.Object,
                _mockMapper.Object,
                _mockArchiveUserService.Object,
                _mockPasswordHashing.Object,
                _mockExcelReader.Object,
                _mockStorage.Object,
                _mockItemRepo.Object,
                _mockLentRepo.Object
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Student MakeStudent(Guid? id = null) => new()
        {
            Id                = id ?? Guid.NewGuid(),
            Username          = "jdelacruz",
            FirstName         = "Juan",
            LastName          = "Dela Cruz",
            Email             = "juan@example.com",
            PhoneNumber       = "09123456789",
            UserRole          = UserRole.Student,
            StudentIdNumber   = "2024-00001",
            Course            = "BSIT",
            Section           = "A",
            Year              = "3",
            Street            = "123 Main St",
            CityMunicipality  = "Quezon City",
            Province          = "Metro Manila",
            PostalCode        = "1100",
            Status            = "Offline"
        };

        private static Teacher MakeTeacher(Guid? id = null) => new()
        {
            Id         = id ?? Guid.NewGuid(),
            Username   = "prof.santos",
            UserRole   = UserRole.Teacher,
            Email      = "santos@example.com",
            Department = "CS"
        };

        private static Staff MakeStaff(Guid? id = null) => new()
        {
            Id       = id ?? Guid.NewGuid(),
            Username = "staff.reyes",
            UserRole = UserRole.Staff,
            Email    = "reyes@example.com",
            Position = "Librarian"
        };

        private static User MakeAdmin(Guid? id = null) => new()
        {
            Id       = id ?? Guid.NewGuid(),
            Username = "admin.garcia",
            UserRole = UserRole.Admin,
            Email    = "garcia@example.com"
        };

        private static User MakeSuperAdmin(Guid? id = null) => new()
        {
            Id       = id ?? Guid.NewGuid(),
            Username = "superadmin",
            UserRole = UserRole.SuperAdmin,
            Email    = "superadmin@example.com"
        };

        // ══════════════════════════════════════════════════════════════════════
        // GET USER PROFILE BY ID
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetUserProfileById_Returns_Null_When_UserNotFound()
        {
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                         .ReturnsAsync((User?)null);

            var result = await _sut.GetUserProfileByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetUserProfileById_Maps_Student_To_StudentProfileDto()
        {
            var student = MakeStudent();
            var expected = new GetStudentProfileDto { FirstName = student.FirstName };

            _mockUserRepo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);
            _mockMapper.Setup(m => m.Map<GetStudentProfileDto>(student)).Returns(expected);

            var result = await _sut.GetUserProfileByIdAsync(student.Id);

            result.Should().BeEquivalentTo(expected);
            _mockMapper.Verify(m => m.Map<GetStudentProfileDto>(student), Times.Once);
        }

        [Fact]
        public async Task GetUserProfileById_Maps_Teacher_To_TeacherProfileDto()
        {
            var teacher = MakeTeacher();
            var expected = new GetTeacherProfileDto { FirstName = teacher.FirstName };

            _mockUserRepo.Setup(r => r.GetByIdAsync(teacher.Id)).ReturnsAsync(teacher);
            _mockMapper.Setup(m => m.Map<GetTeacherProfileDto>(teacher)).Returns(expected);

            var result = await _sut.GetUserProfileByIdAsync(teacher.Id);

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task GetUserProfileById_Maps_Staff_To_StaffProfileDto()
        {
            var staff = MakeStaff();
            var expected = new GetStaffProfileDto { FirstName = staff.FirstName };

            _mockUserRepo.Setup(r => r.GetByIdAsync(staff.Id)).ReturnsAsync(staff);
            _mockMapper.Setup(m => m.Map<GetStaffProfileDto>(staff)).Returns(expected);

            var result = await _sut.GetUserProfileByIdAsync(staff.Id);

            result.Should().BeEquivalentTo(expected);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET ALL USERS
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAllUsers_Returns_All_UserDtos_From_Repository()
        {
            var dtos = new List<UserDto>
            {
                new StudentDto { Username = "student1" },
                new TeacherDto { Username = "teacher1" }
            };

            _mockUserRepo.Setup(r => r.GetAllUserDtosAsync()).ReturnsAsync(dtos);

            var result = await _sut.GetAllUsersAsync();

            result.Should().HaveCount(2);
            _mockUserRepo.Verify(r => r.GetAllUserDtosAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllUsers_Returns_Empty_When_No_Users_Exist()
        {
            _mockUserRepo.Setup(r => r.GetAllUserDtosAsync())
                         .ReturnsAsync(Enumerable.Empty<UserDto>());

            var result = await _sut.GetAllUsersAsync();

            result.Should().BeEmpty();
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET USER BY ID
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetUserById_Returns_Null_When_UserNotFound()
        {
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                         .ReturnsAsync((User?)null);
            _mockMapper.Setup(m => m.Map<UserDto?>(null)).Returns((UserDto?)null);

            var result = await _sut.GetUserByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetUserById_Returns_MappedDto_When_UserFound()
        {
            var student = MakeStudent();
            var dto = new StudentDto { Username = student.Username };

            _mockUserRepo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);
            _mockMapper.Setup(m => m.Map<UserDto?>(student)).Returns(dto);

            var result = await _sut.GetUserByIdAsync(student.Id);

            result.Should().BeEquivalentTo(dto);
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE USER PROFILE (base)
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateUserProfile_Returns_False_When_UserNotFound()
        {
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                         .ReturnsAsync((User?)null);

            var result = await _sut.UpdateUserProfileAsync(Guid.NewGuid(), new UpdateUserProfileDto());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateUserProfile_Returns_True_When_SaveSucceeds()
        {
            var student = MakeStudent();
            var dto = new UpdateUserProfileDto { FirstName = "Updated" };

            _mockUserRepo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.UpdateUserProfileAsync(student.Id, dto);

            result.Should().BeTrue();
            // Mapper is called with (dto, user) — the declared type is User even if runtime is Student
            _mockMapper.Verify(m => m.Map(dto, (User)student), Times.Once);
            _mockUserRepo.Verify(r => r.UpdateAsync(student), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE STUDENT PROFILE
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateStudentProfile_Returns_False_When_UserNotFound()
        {
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                         .ReturnsAsync((User?)null);

            var result = await _sut.UpdateStudentProfileAsync(Guid.NewGuid(), new UpdateStudentProfileDto());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateStudentProfile_Returns_True_When_SaveSucceeds()
        {
            var student = MakeStudent();
            var dto = new UpdateStudentProfileDto { Course = "BSCS" };

            _mockUserRepo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var result = await _sut.UpdateStudentProfileAsync(student.Id, dto);

            result.Should().BeTrue();
            _mockUserRepo.Verify(r => r.UpdateAsync(student), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE STAFF / ADMIN PROFILE
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateStaffOrAdminProfile_Throws_When_CurrentUserNotFound()
        {
            var targetId  = Guid.NewGuid();
            var callerId  = Guid.NewGuid();

            _mockUserRepo.Setup(r => r.GetByIdAsync(callerId)).ReturnsAsync((User?)null);

            Func<Task> act = () => _sut.UpdateStaffOrAdminProfileAsync(targetId, new UpdateStaffProfileDto(), callerId);

            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("*current user*");
        }

        [Fact]
        public async Task UpdateStaffOrAdminProfile_Throws_When_TargetUserNotFound()
        {
            var caller = MakeAdmin();
            var targetId = Guid.NewGuid();

            _mockUserRepo.Setup(r => r.GetByIdAsync(caller.Id)).ReturnsAsync(caller);
            _mockUserRepo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync((User?)null);

            Func<Task> act = () => _sut.UpdateStaffOrAdminProfileAsync(targetId, new UpdateStaffProfileDto(), caller.Id);

            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"*{targetId}*");
        }

        [Fact]
        public async Task UpdateStaffOrAdminProfile_Throws_When_CallerLacksPermission()
        {
            // Staff trying to update another Staff — not allowed
            var caller = MakeStaff();
            var target = MakeStaff();

            _mockUserRepo.Setup(r => r.GetByIdAsync(caller.Id)).ReturnsAsync(caller);
            _mockUserRepo.Setup(r => r.GetByIdAsync(target.Id)).ReturnsAsync(target);

            Func<Task> act = () => _sut.UpdateStaffOrAdminProfileAsync(target.Id, new UpdateStaffProfileDto(), caller.Id);

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*permission*");
        }

        [Fact]
        public async Task UpdateStaffOrAdminProfile_Succeeds_When_Admin_Updates_Staff()
        {
            var admin  = MakeAdmin();
            var staff  = MakeStaff();
            var dto    = new UpdateStaffProfileDto { Position = "Senior Librarian" };

            _mockUserRepo.Setup(r => r.GetByIdAsync(admin.Id)).ReturnsAsync(admin);
            _mockUserRepo.Setup(r => r.GetByIdAsync(staff.Id)).ReturnsAsync(staff);
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            Func<Task> act = () => _sut.UpdateStaffOrAdminProfileAsync(staff.Id, dto, admin.Id);

            await act.Should().NotThrowAsync();
            _mockUserRepo.Verify(r => r.UpdateAsync(staff), Times.Once);
        }

        [Fact]
        public async Task UpdateStaffOrAdminProfile_Succeeds_When_User_Updates_Own_Profile()
        {
            var staff = MakeStaff();
            var dto   = new UpdateStaffProfileDto { Position = "Updated Position" };

            _mockUserRepo.Setup(r => r.GetByIdAsync(staff.Id)).ReturnsAsync(staff);
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            Func<Task> act = () => _sut.UpdateStaffOrAdminProfileAsync(staff.Id, dto, staff.Id);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UpdateStaffOrAdminProfile_Succeeds_When_SuperAdmin_Updates_Admin()
        {
            var superAdmin = MakeSuperAdmin();
            var admin      = MakeAdmin();
            var dto        = new UpdateStaffProfileDto { Email = "new@example.com" };

            _mockUserRepo.Setup(r => r.GetByIdAsync(superAdmin.Id)).ReturnsAsync(superAdmin);
            _mockUserRepo.Setup(r => r.GetByIdAsync(admin.Id)).ReturnsAsync(admin);
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            Func<Task> act = () => _sut.UpdateStaffOrAdminProfileAsync(admin.Id, dto, superAdmin.Id);

            await act.Should().NotThrowAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // DELETE USER (delegates to ArchiveUserService)
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task DeleteUser_Returns_Success_When_Archive_Succeeds()
        {
            var targetId  = Guid.NewGuid();
            var callerId  = Guid.NewGuid();

            _mockArchiveUserService
                .Setup(s => s.ArchiveUserAsync(targetId, callerId))
                .ReturnsAsync((true, string.Empty));

            var (success, error) = await _sut.DeleteUserAsync(targetId, callerId);

            success.Should().BeTrue();
            error.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteUser_Returns_Failure_When_Archive_Fails()
        {
            var targetId = Guid.NewGuid();
            var callerId = Guid.NewGuid();

            _mockArchiveUserService
                .Setup(s => s.ArchiveUserAsync(targetId, callerId))
                .ReturnsAsync((false, "User not found."));

            var (success, error) = await _sut.DeleteUserAsync(targetId, callerId);

            success.Should().BeFalse();
            error.Should().Be("User not found.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // VALIDATE STUDENT PROFILE COMPLETE
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ValidateStudentProfile_Returns_False_When_UserNotFound()
        {
            _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                         .ReturnsAsync((User?)null);

            var (isComplete, error) = await _sut.ValidateStudentProfileComplete(Guid.NewGuid());

            isComplete.Should().BeFalse();
            error.Should().Be("User not found.");
        }

        [Fact]
        public async Task ValidateStudentProfile_Returns_True_For_NonStudent_Users()
        {
            var teacher = MakeTeacher();
            _mockUserRepo.Setup(r => r.GetByIdAsync(teacher.Id)).ReturnsAsync(teacher);

            var (isComplete, error) = await _sut.ValidateStudentProfileComplete(teacher.Id);

            isComplete.Should().BeTrue();
            error.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateStudentProfile_Returns_True_For_Complete_Student()
        {
            var student = MakeStudent(); // all required fields populated
            _mockUserRepo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);

            var (isComplete, _) = await _sut.ValidateStudentProfileComplete(student.Id);

            isComplete.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateStudentProfile_Returns_True_For_Staff()
        {
            var staff = MakeStaff();
            _mockUserRepo.Setup(r => r.GetByIdAsync(staff.Id)).ReturnsAsync(staff);

            var (isComplete, error) = await _sut.ValidateStudentProfileComplete(staff.Id);

            isComplete.Should().BeTrue();
            error.Should().BeEmpty();
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET STUDENT BY ID NUMBER
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetStudentByIdNumber_Returns_Null_When_NotFound()
        {
            _mockUserRepo.Setup(r => r.GetAllAsync())
                         .ReturnsAsync(Enumerable.Empty<User>());

            var result = await _sut.GetStudentByIdNumberAsync("9999-99999");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetStudentByIdNumber_Returns_Student_When_Found()
        {
            var student = MakeStudent();
            _mockUserRepo.Setup(r => r.GetAllAsync())
                         .ReturnsAsync(new List<User> { student });

            var result = await _sut.GetStudentByIdNumberAsync("2024-00001");

            // Service returns an anonymous object — just verify it's not null
            result.Should().NotBeNull();
        }

        // ══════════════════════════════════════════════════════════════════════
        // REGISTER RFID TO STUDENT
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task RegisterRfid_Returns_Success_When_Repository_Succeeds()
        {
            var studentId = Guid.NewGuid();
            const string rfid = "AABBCCDD";

            _mockUserRepo.Setup(r => r.RegisterRfidToStudentAsync(studentId, rfid))
                         .ReturnsAsync((true, string.Empty));
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var (success, error) = await _sut.RegisterRfidToStudentAsync(studentId, rfid);

            success.Should().BeTrue();
            error.Should().BeEmpty();
        }

        [Fact]
        public async Task RegisterRfid_Returns_Failure_When_Repository_Fails()
        {
            var studentId = Guid.NewGuid();
            const string rfid = "AABBCCDD";

            _mockUserRepo.Setup(r => r.RegisterRfidToStudentAsync(studentId, rfid))
                         .ReturnsAsync((false, "RFID already registered."));

            var (success, error) = await _sut.RegisterRfidToStudentAsync(studentId, rfid);

            success.Should().BeFalse();
            error.Should().Be("RFID already registered.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET STUDENT BY RFID UID
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetStudentByRfid_Returns_Null_When_NotFound()
        {
            _mockUserRepo.Setup(r => r.GetStudentByRfidUidAsync(It.IsAny<string>()))
                         .ReturnsAsync((Student?)null);

            var result = await _sut.GetStudentByRfidUidAsync("UNKNOWN");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetStudentByRfid_Returns_Student_When_Found()
        {
            var student = MakeStudent();
            student.RfidUid = "AABBCCDD";

            _mockUserRepo.Setup(r => r.GetStudentByRfidUidAsync("AABBCCDD"))
                         .ReturnsAsync(student);

            var result = await _sut.GetStudentByRfidUidAsync("AABBCCDD");

            result.Should().NotBeNull();
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE STUDENT PROFILE — image upload
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateStudentProfile_UploadsProfilePicture_WhenImageProvided()
        {
            // Arrange
            var student = MakeStudent();
            student.ProfilePictureUrl = "https://cdn.example.com/old.jpg";

            var mockFile = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
            mockFile.Setup(f => f.Length).Returns(512);
            mockFile.Setup(f => f.FileName).Returns("photo.jpg");
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[512]));

            var dto = new UpdateStudentProfileDto { ProfilePicture = mockFile.Object };

            _mockUserRepo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockStorage.Setup(s => s.DeleteImageAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockStorage.Setup(s => s.UploadImageAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), "students/profile"))
                        .ReturnsAsync("https://cdn.example.com/new.jpg");

            // Act
            var result = await _sut.UpdateStudentProfileAsync(student.Id, dto);

            // Assert
            result.Should().BeTrue();
            _mockStorage.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Once);
            _mockStorage.Verify(s => s.UploadImageAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), "students/profile"), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET USER ITEM SUMMARY
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetUserItemSummary_Returns_CorrectCounts_ForStudentWithMixedStatuses()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var lentItems = new List<LentItems>
            {
                new() { Id = Guid.NewGuid(), UserId = userId, Status = "Pending" },
                new() { Id = Guid.NewGuid(), UserId = userId, Status = "Approved" },
                new() { Id = Guid.NewGuid(), UserId = userId, Status = "Borrowed" },
                new() { Id = Guid.NewGuid(), UserId = userId, Status = "Returned" },
                new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = "Borrowed" } // different user — excluded
            };

            var items = new List<Item>
            {
                new() { Id = Guid.NewGuid(), Status = Enums.ItemStatus.Available },
                new() { Id = Guid.NewGuid(), Status = Enums.ItemStatus.Available },
                new() { Id = Guid.NewGuid(), Status = Enums.ItemStatus.Borrowed }
            };

            _mockLentRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(lentItems);
            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(items);

            // Act
            var result = await _sut.GetUserItemSummaryAsync(userId);

            // Assert — Pending + Approved = 2 reserved, 1 borrowed, 1 returned, 2 available items
            result.ReservedCount.Should().Be(2);
            result.BorrowedCount.Should().Be(1);
            result.ReturnedCount.Should().Be(1);
            result.AvailableCount.Should().Be(2);
        }

        [Fact]
        public async Task GetUserItemSummary_Returns_ZeroCounts_WhenStudentHasNoHistory()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockLentRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<LentItems>());
            _mockItemRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<Item>());

            // Act
            var result = await _sut.GetUserItemSummaryAsync(userId);

            // Assert
            result.ReservedCount.Should().Be(0);
            result.BorrowedCount.Should().Be(0);
            result.ReturnedCount.Should().Be(0);
            result.AvailableCount.Should().Be(0);
        }

        // ══════════════════════════════════════════════════════════════════════
        // IMPORT STUDENTS FROM EXCEL
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ImportStudentsFromExcelAsync_ReturnsFailure_WhenFileInvalid()
        {
            // Arrange
            var mockFile = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
            var emptyStudents = new List<(string, string, string?, int)>();

            _mockExcelReader
                .Setup(r => r.ReadStudentsFromExcelAsync(mockFile.Object))
                .ReturnsAsync((emptyStudents, "Excel file must contain 'LastName' and 'FirstName' columns."));

            // Act
            var result = await _sut.ImportStudentsFromExcelAsync(mockFile.Object);

            // Assert
            result.Errors.Should().ContainSingle()
                .Which.Should().Contain("LastName");
            result.FailureCount.Should().Be(0); // empty list, so 0 processed
        }

        [Fact]
        public async Task ImportStudentsFromExcelAsync_ReturnsDetailedResults_OnPartialSuccess()
        {
            // Arrange
            var mockFile = new Mock<Microsoft.AspNetCore.Http.IFormFile>();

            // One valid row, one row with empty FirstName
            var students = new List<(string FirstName, string LastName, string? MiddleName, int RowNumber)>
            {
                ("Juan",   "Dela Cruz", null, 2),
                ("",       "Santos",    null, 3)  // invalid — empty first name
            };

            _mockExcelReader
                .Setup(r => r.ReadStudentsFromExcelAsync(mockFile.Object))
                .ReturnsAsync((students, null));

            // Repo: no existing user with same name
            _mockUserRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<User>());
            _mockUserRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(new User());
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockPasswordHashing.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");

            // Act
            var result = await _sut.ImportStudentsFromExcelAsync(mockFile.Object);

            // Assert
            result.SuccessCount.Should().Be(1);
            result.FailureCount.Should().Be(1);
            result.Errors.Should().ContainSingle()
                .Which.Should().Contain("Row 3");
        }
    }
}
