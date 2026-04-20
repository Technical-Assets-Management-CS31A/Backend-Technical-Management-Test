# Comprehensive xUnit Test Strategy & Checklist

**Project:** BackendTechnicalAssetsManagement
**Test Framework:** xUnit + Moq + FluentAssertions
**Test Project:** `BackendTechincalAssetsManagementTest`

---

## Core Principles

1. **Zero Latency** — All repositories and external I/O fully mocked. Data objects are hardcoded POCOs.
2. **<200ms Execution** — No `Thread.Sleep`, `Task.Delay`, or real cryptographic hashing in standard tests.
3. **Database Bypass** — No EF Core (not even In-Memory DB). Mock repositories with Moq.

---

## Project Structure

```
BackendTechincalAssetsManagementTest/
├── Services/
│   ├── AuthServiceTests.cs         ✅ Part 1
│   ├── UserServiceTests.cs         Part 2
│   ├── ItemServiceTests.cs         Part 3
│   ├── LentItemsServiceTests.cs    Part 4
│   ├── ActivityLogServiceTests.cs  Part 5
│   ├── SummaryServiceTests.cs      Part 6
│   ├── ArchiveServiceTests.cs      Part 7
│   ├── NotificationServiceTests.cs Part 8
│   └── SupabaseStorageTests.cs     Part 9
└── Utilities/
    └── UtilityServiceTests.cs      Part 10
```

---

## xUnit Implementation Pattern

Every test class follows this structure:

```csharp
public class XxxServiceTests
{
    // 1. Declare mocks as readonly fields
    private readonly Mock<IXxxRepository> _mockRepo;
    private readonly XxxService _sut; // System Under Test

    // 2. Constructor = test setup (replaces [SetUp] from NUnit)
    public XxxServiceTests()
    {
        _mockRepo = new Mock<IXxxRepository>();
        _sut = new XxxService(_mockRepo.Object);
    }

    // 3. [Fact] = single test case
    [Fact]
    public async Task MethodName_ExpectedBehavior_WhenCondition()
    {
        // Arrange — set up mocks and input data
        // Act     — call the method under test
        // Assert  — verify with FluentAssertions
    }

    // 4. [Theory] + [InlineData] = parameterized test
    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Teacher)]
    public async Task MethodName_Behavior(UserRole role) { ... }
}
```

### Common Mock Patterns

```csharp
// Return a value
_mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(user);

// Return null
_mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((User?)null);

// Void async method
_mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

// Throw exception
_mockRepo.Setup(r => r.GetByIdAsync(id)).ThrowsAsync(new Exception("fail"));

// Verify a method was called once
_mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);

// Verify never called
_mockRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);

// Capture argument passed to mock
User? captured = null;
_mockRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
         .Callback<User>(u => captured = u)
         .ReturnsAsync(new User());
```

### Common FluentAssertions Patterns

```csharp
// Null / not null
result.Should().NotBeNull();
result.Should().BeNull();

// Equality
result.Id.Should().Be(expectedId);
result.Should().BeEquivalentTo(expectedDto);

// Collections
list.Should().HaveCount(3);
list.Should().ContainSingle();
list.Should().BeEmpty();

// Exceptions
Func<Task> act = () => _sut.DoSomethingAsync(dto);
await act.Should().ThrowAsync<UnauthorizedAccessException>()
    .WithMessage("*some message*");

// Not throw
await act.Should().NotThrowAsync();

// Type checking
result.Should().BeOfType<StudentDto>();
result.Should().BeAssignableTo<UserDto>();

// String
result.Token.Should().NotBeNullOrEmpty();
result.Message.Should().Contain("invalid");
```

---

## 1. Authentication & Identity (`AuthService`)

**File:** `Services/AuthServiceTests.cs`
**Mocks:** `IUserRepository`, `IRefreshTokenRepository`, `IPasswordHashingService`, `IUserValidationService`, `IMapper`, `IConfiguration`, `IHttpContextAccessor`, `IWebHostEnvironment`, `IDevelopmentLoggerService`

> `AppDbContext` is passed as `null!` — `AuthService` never touches it directly; all DB work goes through repositories.

> JWT key must be ≥ 64 characters for HMAC-SHA512.

```csharp
// Constructor setup pattern for AuthService
_sut = new AuthService(
    null!,                          // AppDbContext — unused, all DB via repos
    _mockConfig.Object,
    _mockHttpContextAccessor.Object,
    _mockPasswordHashing.Object,
    _mockUserRepo.Object,
    _mockMapper.Object,
    _mockUserValidation.Object,
    _mockEnv.Object,
    _mockDevLogger.Object,
    _mockRefreshTokenRepo.Object
);

// Mock HttpContext for cookie-based tests
var resCookies = new Mock<IResponseCookies>();
var response   = new Mock<HttpResponse>();
response.Setup(r => r.Cookies).Returns(resCookies.Object);
var ctx = new Mock<HttpContext>();
ctx.Setup(c => c.Response).Returns(response.Object);
_mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(ctx.Object);

// Mock ClaimsPrincipal for ChangePassword tests
var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
{
    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
    new Claim(ClaimTypes.Role, "Admin")
}, "TestAuth"));
ctx.Setup(c => c.User).Returns(claims);
```

- [x] `Register_Fails_When_UserRoleHierarchyIsViolated`
- [x] `Register_Fails_When_PhoneNumberDoesNotStartWith09`
- [x] `Register_Fails_When_PasswordDoesNotMeetComplexityRules`
- [x] `Register_Fails_When_PhoneNumberIsAlreadyUsed`
- [x] `Register_Succeeds_And_InstantiatesCorrectDerivedClass` — `[Theory][InlineData(Student/Teacher/Staff)]`
- [x] `Login_ThrowsInvalidCredentials_WhenUserNotFound`
- [x] `Login_ThrowsInvalidCredentials_WhenPasswordHashFails`
- [x] `Login_Succeeds_RevokesOldTokens_ReturnsNewTokens`
- [x] `LoginMobile_Succeeds_ReturnsTokensInDTOWithoutCookies`
- [x] `ChangePassword_ThrowsUnauthorized_WhenAdminChangesSuperAdmin`
- [x] `ChangePassword_Succeeds_HashesNewPassword_And_RevokesAllTokens`
- [x] `RefreshToken_ThrowsException_IfCookieIsMissing`
- [x] `RefreshToken_DetectsReplayAttack_IfTokenIsAlreadyRevoked`

---

## 2. User Management (`UserService`)

**File:** `Services/UserServiceTests.cs`
**Mocks:** `IUserRepository`, `IArchiveUserService`, `IExcelReaderService`, `ISupabaseStorageService`, `IMapper`, `IHttpContextAccessor`

```csharp
// Mapper setup for polymorphic profile DTOs
_mockMapper.Setup(m => m.Map<StudentDto>(It.IsAny<Student>()))
           .Returns(new StudentDto { ... });

// IFormFile mock for image upload tests
var mockFile = new Mock<IFormFile>();
mockFile.Setup(f => f.Length).Returns(1024);
mockFile.Setup(f => f.FileName).Returns("photo.jpg");
mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));
```

- [ ] `GetUserProfileByIdAsync_ReturnsStudentProfileDto_WhenUserIsStudent`
- [ ] `GetUserProfileByIdAsync_ReturnsTeacherProfileDto_WhenUserIsTeacher`
- [ ] `GetUserProfileByIdAsync_ReturnsStaffProfileDto_WhenUserIsStaff`
- [ ] `GetUserProfileByIdAsync_ReturnsNull_WhenUserDoesNotExist`
- [ ] `GetAllUsersAsync_ReturnsExpectedCollection`
- [ ] `UpdateStudentProfileAsync_ReturnsFalse_WhenStudentNotFound`
- [ ] `UpdateStudentProfileAsync_ReturnsTrue_WhenUpdateSucceeds`
- [ ] `UpdateStudentProfileAsync_UploadsProfilePicture_WhenImageProvided`
- [ ] `UpdateTeacherProfileAsync_ReturnsTrue_WhenUpdateSucceeds`
- [ ] `UpdateStaffOrAdminProfileAsync_ThrowsUnauthorized_IfRoleHierarchyViolated`
- [ ] `DeleteUserAsync_ReturnsFailure_WhenUserNotFound`
- [ ] `DeleteUserAsync_ReturnsSuccess_WhenArchivedAndDeleted`
- [ ] `ImportStudentsFromExcelAsync_ReturnsFailure_WhenFileInvalid`
- [ ] `ImportStudentsFromExcelAsync_ReturnsDetailedResults_OnPartialSuccess`
- [ ] `ValidateStudentProfileComplete_ReturnsFalse_WhenFieldsAreMissing`
- [ ] `GetStudentByIdNumberAsync_ReturnsMatch_OrNull`
- [ ] `RegisterRfidToStudentAsync_ReturnsFalse_WhenRfidAlreadyAssigned`
- [ ] `RegisterRfidToStudentAsync_ReturnsTrue_OnSuccess`
- [ ] `GetStudentByRfidUidAsync_ReturnsMatch_OrNull`

---

## 3. Inventory & Items (`ItemService`)

**File:** `Services/ItemServiceTests.cs`
**Mocks:** `IItemRepository`, `IArchiveItemsService`, `ILentItemsRepository`, `ISupabaseStorageService`, `IMapper`

```csharp
// Serial number normalization test pattern
var dto = new CreateItemDto { SerialNumber = "sn-001" };
var result = await _sut.CreateItemAsync(dto);
result.SerialNumber.Should().Be("SN-001"); // uppercased

// Duplicate serial number — mock repo to return existing item
_mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-001"))
             .ReturnsAsync(new Item { SerialNumber = "SN-001" });
```

- [ ] `GetAllItemsAsync_ReturnsValidItemCollection`
- [ ] `GetItemByIdAsync_ReturnsItem_WhenMatchFound`
- [ ] `GetItemBySerialNumberAsync_ReturnsMatch_OrNull`
- [ ] `GetItemByRfidUidAsync_ReturnsMatch_OrNull`
- [ ] `CreateItemAsync_ThrowsDuplicateSerialNumberException_IfConflictExists`
- [ ] `CreateItemAsync_AutoPrefixesSerialNumber_WhenSnPrefixMissing`
- [ ] `CreateItemAsync_NormalizesSerialNumber_ToUppercase`
- [ ] `CreateItemAsync_UploadsImage_WhenImageProvided`
- [ ] `CreateItemAsync_ReturnsCreatedItem_OnSuccess`
- [ ] `UpdateItemAsync_ReturnsFalse_WhenItemNotFound`
- [ ] `UpdateItemAsync_ReturnsTrue_AndPersistsChanges`
- [ ] `UpdateItemAsync_DeletesOldImage_WhenNewImageProvided`
- [ ] `UpdateItemLocationAsync_ReturnsTrue_WhenItemExists`
- [ ] `UpdateItemLocationAsync_ReturnsFalse_WhenItemNotFound`
- [ ] `DeleteItemAsync_ReturnsSuccessTuple_WhenArchivedAndDeleted`
- [ ] `ImportItemsFromExcelAsync_ParsesXlsx_AndCreatesItemsSuccessfully`
- [ ] `ImportItemsFromExcelAsync_ReturnsPartialSuccess_WhenSomeRowsHaveErrors`
- [ ] `ScanRfidAsync_ReturnsFailure_WhenNoItemLinked`
- [ ] `ScanRfidAsync_ReturnsSuccess_WithNewStatus_OnMatch`
- [ ] `RegisterRfidToItemAsync_ReturnsFalse_WhenRfidAlreadyAssigned`
- [ ] `RegisterRfidToItemAsync_ReturnsFalse_WhenItemNotFound`
- [ ] `RegisterRfidToItemAsync_ReturnsTrue_OnSuccess`

---

## 4. Lending & Circulation (`LentItemsService`)

**File:** `Services/LentItemsServiceTests.cs`
**Mocks:** `ILentItemsRepository`, `IItemRepository`, `IUserRepository`, `IActivityLogService`, `INotificationService`, `ISupabaseStorageService`, `IMapper`, `IHttpContextAccessor`

```csharp
// Item condition guard test pattern
var item = new Item { Condition = ItemCondition.Defective };
_mockItemRepo.Setup(r => r.GetByIdAsync(itemId)).ReturnsAsync(item);
Func<Task> act = () => _sut.AddAsync(dto);
await act.Should().ThrowAsync<InvalidOperationException>();

// Verify activity log was written
_mockActivityLogService.Verify(
    s => s.LogAsync(It.IsAny<CreateActivityLogDto>()), Times.Once);

// Verify notification was sent
_mockNotificationService.Verify(
    s => s.SendNewPendingRequestNotificationAsync(It.IsAny<string>()), Times.Once);
```

### 4a. Standard Borrow Flow (`AddAsync`)

- [ ] `AddAsync_ThrowsInvalidOperation_WhenItemIsDefective`
- [ ] `AddAsync_ThrowsInvalidOperation_WhenItemIsNeedRepair`
- [ ] `AddAsync_ThrowsInvalidOperation_WhenItemIsAlreadyBorrowed`
- [ ] `AddAsync_ThrowsInvalidOperation_WhenItemIsAlreadyReserved`
- [ ] `AddAsync_ThrowsInvalidOperation_WhenActiveLentRecordExists`
- [ ] `AddAsync_ThrowsInvalidOperation_WhenReservationTimeSlotConflicts`
- [ ] `AddAsync_ReturnsCreatedLentItem_WhenValidRequest`
- [ ] `AddAsync_WritesActivityLog_OnSuccess`
- [ ] `AddAsync_SendsNotification_OnSuccess`

### 4b. Guest Borrow Flow (`AddForGuestAsync`)

- [ ] `AddForGuestAsync_ThrowsInvalidOperation_WhenTagUidNotFound`
- [ ] `AddForGuestAsync_ThrowsInvalidOperation_WhenItemIsDefectiveOrNeedRepair`
- [ ] `AddForGuestAsync_ThrowsInvalidOperation_WhenItemIsAlreadyBorrowed`
- [ ] `AddForGuestAsync_SetsBorrowerRole_ToGuest_Always`
- [ ] `AddForGuestAsync_StoresOrganization_ContactNumber_Purpose`
- [ ] `AddForGuestAsync_UploadsGuestImage_WhenImageProvided`
- [ ] `AddForGuestAsync_SetsIssuedById_FromCallerIdentity`
- [ ] `AddForGuestAsync_ReturnsCreatedLentItem_WhenValid`
- [ ] `AddForGuestAsync_WritesActivityLog_OnSuccess`

### 4c. Updates & Status Transitions

- [ ] `UpdateAsync_ReturnsFalse_WhenLentItemNotFound`
- [ ] `UpdateAsync_ReturnsTrue_WhenValidUpdateApplied`
- [ ] `UpdateAsync_WritesActivityLog_WhenStatusChanges`
- [ ] `UpdateAsync_SendsNotification_WhenStatusChanges`
- [ ] `UpdateStatusAsync_TransitionsStatus_AndWritesLog`
- [ ] `UpdateStatusAsync_ReturnsFalse_WhenLentItemNotFound`

### 4d. Visibility & Archival

- [ ] `UpdateHistoryVisibility_ReturnsTrue_WhenUserOwnsRecord`
- [ ] `UpdateHistoryVisibility_ReturnsFalse_WhenUserDoesNotOwnRecord`
- [ ] `UpdateHistoryVisibility_ReturnsFalse_WhenLentItemNotFound`
- [ ] `ArchiveLentItems_ReturnsSuccess_WhenItemExists`
- [ ] `ArchiveLentItems_ReturnsFailure_WhenItemNotFound`
- [ ] `ArchiveLentItems_ReturnsFailure_WhenItemIsStillBorrowed`

### 4e. Queries

- [ ] `GetAllAsync_ReturnsAllLentItems`
- [ ] `GetAllBorrowedItemsAsync_ReturnsOnlyBorrowedStatus`
- [ ] `GetByIdAsync_ReturnsItem_WhenFound`
- [ ] `GetByIdAsync_ReturnsNull_WhenNotFound`
- [ ] `GetByDateTimeAsync_FiltersCorrectly_GivenUtcDateTime`

### 4f. Background / Expiry

- [ ] `CancelExpiredReservationsAsync_IdentifiesAndCancels_StaleReservations_ReturnsCount`
- [ ] `CancelExpiredReservationsAsync_WritesActivityLog_ForEachCanceled`
- [ ] `IsItemAvailableForReservation_ReturnsFalse_WhenSlotConflicts`
- [ ] `IsItemAvailableForReservation_ReturnsTrue_WhenNoConflict`

---

## 5. Activity Logs (`ActivityLogService`)

**File:** `Services/ActivityLogServiceTests.cs`
**Mocks:** `IActivityLogRepository`

```csharp
// Filter test pattern — return a fixed list, assert filtered result
var logs = new List<ActivityLog>
{
    new() { Category = ActivityLogCategory.BorrowedItem, ActorUserId = userId },
    new() { Category = ActivityLogCategory.Returned,     ActorUserId = otherUserId }
};
_mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(logs);

var result = await _sut.GetAllAsync(actorUserId: userId);
result.Should().ContainSingle();
result.First().ActorUserId.Should().Be(userId);
```

- [ ] `GetAllAsync_ReturnsAllLogs_WhenNoFilterApplied`
- [ ] `GetAllAsync_FiltersBy_Category_WhenProvided`
- [ ] `GetAllAsync_FiltersBy_DateRange_WhenFromAndToProvided`
- [ ] `GetAllAsync_FiltersBy_ActorUserId_WhenProvided`
- [ ] `GetAllAsync_FiltersBy_ItemId_WhenProvided`
- [ ] `GetAllAsync_FiltersBy_Status_WhenProvided`
- [ ] `GetByIdAsync_ReturnsLog_WhenFound`
- [ ] `GetByIdAsync_ReturnsNull_WhenNotFound`
- [ ] `GetBorrowLogsAsync_ReturnsBorrowedAndReturnedCategories`
- [ ] `GetBorrowLogsAsync_FiltersBy_DateRange_UserId_ItemId`

---

## 6. Statistical Summaries (`SummaryService`)

**File:** `Services/SummaryServiceTests.cs`
**Mocks:** `IItemRepository`, `ILentItemsRepository`, `IUserRepository`

```csharp
// Return predefined counts from mocked repos
_mockItemRepo.Setup(r => r.GetAllAsync())
             .ReturnsAsync(new List<Item>
             {
                 new() { Condition = ItemCondition.Good },
                 new() { Condition = ItemCondition.Defective }
             });

var result = await _sut.GetItemCountAsync();
result.Functional.Should().Be(1);
result.Defective.Should().Be(1);
```

- [ ] `GetItemCountAsync_CalculatesAccurate_FunctionalVsDefectiveCounts`
- [ ] `GetLentItemsCountAsync_SplitsActiveVersusReturnedProperly`
- [ ] `GetActiveUserCountAsync_SeparatesRoles_IntoCorrectBuckets`
- [ ] `GetOverallSummaryAsync_Aggregates_AllSubSummaries_IntoSingleDto`

---

## 7. Archival Services

**File:** `Services/ArchiveServiceTests.cs`
**Mocks:** `IArchiveItemRepository`, `IArchiveLentItemsRepository`, `IArchiveUserRepository`, `IItemRepository`, `ILentItemsRepository`, `IUserRepository`, `IMapper`

```csharp
// Archive mapping test — capture what was passed to AddAsync
ArchiveItem? captured = null;
_mockArchiveRepo.Setup(r => r.AddAsync(It.IsAny<ArchiveItem>()))
                .Callback<ArchiveItem>(a => captured = a)
                .Returns(Task.CompletedTask);

await _sut.CreateItemArchiveAsync(item);

captured.Should().NotBeNull();
captured!.SerialNumber.Should().Be(item.SerialNumber);
```

- [ ] `CreateItemArchiveAsync_SuccessfullyMapsAndSavesToArchiveModel`
- [ ] `GetItemArchiveByIdAsync_RetrievesSpecificArchiveRecord`
- [ ] `RestoreItemAsync_NullifiesArchivedFlag_AndReinsertsToActiveTable`
- [ ] `DeleteItemArchiveAsync_PermanentlyRemovesFromArchiveTable`
- [ ] `CreateLentItemsArchiveAsync_MapsGuestFields_Organization_ContactNumber_Purpose`
- [ ] `CreateLentItemsArchiveAsync_MapsGuestImageUrl_WhenPresent`
- [ ] `GetLentItemsArchiveByIdAsync_RetrievesSpecificArchiveRecord`
- [ ] `RestoreLentItemsAsync_ReturnsNull_WhenArchiveNotFound`
- [ ] `RestoreLentItemsAsync_ReturnsRestoredDto_OnSuccess`
- [ ] `DeleteLentItemsArchiveAsync_ReturnsFalse_WhenNotFound`
- [ ] `CreateUserArchiveAsync_SuccessfullyMapsAndSavesToArchiveModel`
- [ ] `GetArchivedUserByIdAsync_ReturnsNull_WhenNotFound`
- [ ] `RestoreUserAsync_ReturnsFalse_WhenArchiveNotFound`
- [ ] `RestoreUserAsync_ReturnsTrue_OnSuccess`
- [ ] `DeleteUserArchiveAsync_PermanentlyRemovesFromArchiveTable`

---

## 8. Real-Time Notifications (`NotificationService`)

**File:** `Services/NotificationServiceTests.cs`
**Mocks:** `IHubContext<NotificationHub>`, `IHubClients`, `IClientProxy`

```csharp
// SignalR mock pattern — verify SendAsync was called
var mockClientProxy = new Mock<IClientProxy>();
var mockClients     = new Mock<IHubClients>();
var mockHubContext  = new Mock<IHubContext<NotificationHub>>();

mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

await _sut.SendBroadcastNotificationAsync("message");

mockClientProxy.Verify(
    c => c.SendCoreAsync("ReceiveNotification",
        It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
    Times.Once);
```

- [ ] `SendNewPendingRequestNotificationAsync_Verifies_SignalR_SendAsync_Invocation`
- [ ] `SendApprovalNotificationAsync_Verifies_TargetedUserIdDelivery`
- [ ] `SendStatusChangeNotificationAsync_FormatsMessageStringCorrectly`
- [ ] `SendBroadcastNotificationAsync_Invokes_AllClients`

---

## 9. Storage Service (`SupabaseStorageService`)

**File:** `Services/SupabaseStorageTests.cs`
**Mocks:** `ISupabaseStorageService` (mock the interface, test consuming services)

```csharp
// Verify upload is called when image is provided
_mockStorage.Setup(s => s.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/image.jpg");

await _sut.CreateItemAsync(dtoWithImage);

_mockStorage.Verify(
    s => s.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Once);

// Verify delete is NOT called when no existing image
_mockStorage.Verify(
    s => s.DeleteImageAsync(It.IsAny<string>()), Times.Never);
```

- [ ] `UploadImageAsync_ReturnsPublicUrl_OnSuccess` _(integration-style, skip in unit suite)_
- [ ] `DeleteImageAsync_DoesNotThrow_WhenUrlIsNullOrEmpty`
- [ ] `DeleteImageAsync_ExtractsCorrectPath_FromPublicUrl`
- [ ] `ItemService_CreateItemAsync_CallsUploadImageAsync_WhenImageIsProvided`
- [ ] `ItemService_UpdateItemAsync_CallsDeleteImageAsync_WhenReplacingExistingImage`
- [ ] `UserService_UpdateStudentProfileAsync_CallsUploadImageAsync_WhenImageIsProvided`
- [ ] `LentItemsService_AddForGuestAsync_CallsUploadImageAsync_WhenGuestImageIsProvided`

---

## 10. Utility & Infrastructure Services

**File:** `Utilities/UtilityServiceTests.cs`
**No mocks needed** — pure logic tests.

```csharp
// Password hashing — use the real service, just verify behavior
var sut = new PasswordHashingService();
var hash = sut.HashPassword("Password1!");
hash.Should().NotBe("Password1!");
sut.VerifyPassword("Password1!", hash).Should().BeTrue();
sut.VerifyPassword("WrongPass",  hash).Should().BeFalse();

// Background service cancellation test
using var cts = new CancellationTokenSource();
cts.Cancel(); // cancel immediately
await _sut.StartAsync(cts.Token);
_mockLentItemsService.Verify(
    s => s.CancelExpiredReservationsAsync(), Times.Never);
```

### Excel Reader (`ExcelReaderService`)

- [ ] `Parse_ThrowsException_IfFileIsNotXlsx`
- [ ] `Parse_ReadsWorksheets_AndYieldsExpectedDictionaryMap`

### Password Hashing (`PasswordHashingService`)

- [ ] `HashPassword_ProducesHash_DifferentFromPlaintext`
- [ ] `VerifyPassword_ReturnsTrue_ForCorrectHash_AndFalseForIncorrect`

### File Validation (`FileValidationUtils`)

- [ ] `ValidateImportFileAsync_ReturnsInvalid_WhenExtensionIsNotXlsxOrCsv`
- [ ] `ValidateImportFileAsync_ReturnsInvalid_WhenMagicBytesDoNotMatchExtension`
- [ ] `ValidateImportFileAsync_ReturnsValid_ForLegitimateXlsxFile`

### Image Validation (`ImageConverterUtils`)

- [ ] `ValidateImage_ThrowsArgumentException_WhenImageExceedsSizeLimit`
- [ ] `ValidateImage_ThrowsArgumentException_WhenFormatIsNotAllowed`
- [ ] `ValidateImage_DoesNotThrow_ForValidJpegOrPng`

### Background Jobs

- [ ] `ExecuteAsync_TriggersCleanup_AccordingToConfiguredDelayInterval`
- [ ] `ExecuteAsync_SkipsIfCancellationRequested_AtStartup`
- [ ] `ReservationExpiryBackgroundService_CallsCancelExpiredReservationsAsync_OnEachCycle`
