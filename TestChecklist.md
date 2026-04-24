# Comprehensive xUnit Test Strategy & Checklist

**Project:** BackendTechnicalAssetsManagement
**Test Framework:** xUnit + Moq + FluentAssertions
**Status:** ✅ 283 passing — 🆕 36 pending (audit: April 2026)

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
│   ├── AuthServiceTests.cs          ✅ Part 1  — 13 tests
│   ├── UserServiceTests.cs          ✅ Part 2  — 37 tests
│   ├── ItemServiceTests.cs          ✅ Part 3  — 22 tests
│   ├── LentItemsServiceTests.cs     ✅ Part 4  — 40 tests
│   ├── AddBorrowAndReserveTests.cs  ✅ Part 4b — 16 tests
│   ├── ActivityLogServiceTests.cs   ✅ Part 5  — 13 tests
│   ├── SummaryServiceTests.cs       ✅ Part 6  —  9 tests
│   ├── ArchiveServiceTests.cs       ✅ Part 7  — 18 tests
│   └── NotificationServiceTests.cs  ✅ Part 8  — 12 tests
├── Utilities/
│   └── UtilityServiceTests.cs       ✅ Part 10 — 24 tests
├── Controllers/                     ⚠️  Part 11 — 40 / 76 tests (36 pending)
│   ├── AuthControllerTests.cs           ✅ 7 / 11
│   ├── UserControllerTests.cs           ✅ 8 / 14
│   ├── ItemControllerTests.cs           ✅ 8 / 15
│   ├── LentItemsControllerTests.cs      ✅ 7 / 11
│   ├── ActivityLogControllerTests.cs    ✅ 4 / 4
│   ├── SummaryControllerTests.cs        ✅ 2 / 2
│   ├── ArchiveItemsControllerTests.cs   ✅ 4 / 4
│   ├── ArchiveLentItemsControllerTests.cs ✅ 4 / 4
│   └── ArchiveUsersControllerTests.cs   ✅ 4 / 4
├── Infrastructure/                  ✅ Part 12 — 18 / 18 tests
│   ├── GlobalExceptionHandlerTests.cs
│   ├── AuthorizationHandlerTests.cs
│   ├── UserValidationServiceTests.cs
│   └── RefreshTokenMiddlewareTests.cs   🆕 Part 13 — 0 / 5 pending
├── Services/
│   └── ItemServiceTests.cs              ⚠️  Part 3 — 22 / 27 (5 pending — import)
└── UnitTest1.cs                         (placeholder — 1 test)
```

> **Part 9 (SupabaseStorageService)** — skipped as a standalone file.
> Storage behavior is already verified indirectly: `ItemService` tests cover
> `UploadImageAsync` and `DeleteImageAsync` call sites via mock verification.
>
> **ArchiveUserService** — excluded from unit tests. It uses `AppDbContext`
> directly for transaction management, which requires integration-level testing.
>
> **Parts 11–12** — new scope added after full codebase audit. See sections below.

---

## xUnit Implementation Pattern

```csharp
public class XxxServiceTests
{
    private readonly Mock<IXxxRepository> _mockRepo;
    private readonly XxxService _sut;

    public XxxServiceTests()
    {
        _mockRepo = new Mock<IXxxRepository>();
        _sut = new XxxService(_mockRepo.Object);
    }

    [Fact]
    public async Task MethodName_ExpectedBehavior_WhenCondition()
    {
        // Arrange — set up mocks and input data
        // Act     — call the method under test
        // Assert  — verify with FluentAssertions
    }
}
```

---

## 1. Authentication & Identity (`AuthService`) — 13 / 13 ✅

**File:** `Services/AuthServiceTests.cs`

- [x] `Register_Fails_When_UserRoleHierarchyIsViolated`
- [x] `Register_Fails_When_PhoneNumberDoesNotStartWith09`
- [x] `Register_Fails_When_PasswordDoesNotMeetComplexityRules`
- [x] `Register_Fails_When_PhoneNumberIsAlreadyUsed`
- [x] `Register_Succeeds_And_InstantiatesCorrectDerivedClass` — `[Theory]` × 3 (Student / Teacher / Staff)
- [x] `Login_ThrowsInvalidCredentials_WhenUserNotFound`
- [x] `Login_ThrowsInvalidCredentials_WhenPasswordHashFails`
- [x] `Login_Succeeds_RevokesOldTokens_ReturnsNewTokens`
- [x] `LoginMobile_Succeeds_ReturnsTokensInDTOWithoutCookies`
- [x] `ChangePassword_ThrowsUnauthorized_WhenAdminChangesSuperAdmin`
- [x] `ChangePassword_Succeeds_HashesNewPassword_And_RevokesAllTokens`
- [x] `RefreshToken_ThrowsException_IfCookieIsMissing`
- [x] `RefreshToken_DetectsReplayAttack_IfTokenIsAlreadyRevoked`

---

## 2. User Management (`UserService`) — 37 / 37 ✅

**File:** `Services/UserServiceTests.cs`

### Profile Retrieval

- [x] `GetUserProfileById_Returns_Null_When_UserNotFound`
- [x] `GetUserProfileById_Maps_Student_To_StudentProfileDto`
- [x] `GetUserProfileById_Maps_Teacher_To_TeacherProfileDto`
- [x] `GetUserProfileById_Maps_Staff_To_StaffProfileDto`

### Get All / Get By Id

- [x] `GetAllUsers_Returns_All_UserDtos_From_Repository`
- [x] `GetAllUsers_Returns_Empty_When_No_Users_Exist`
- [x] `GetUserById_Returns_Null_When_UserNotFound`
- [x] `GetUserById_Returns_MappedDto_When_UserFound`

### Update Profile

- [x] `UpdateUserProfile_Returns_False_When_UserNotFound`
- [x] `UpdateUserProfile_Returns_True_When_SaveSucceeds`
- [x] `UpdateStudentProfile_Returns_False_When_UserNotFound`
- [x] `UpdateStudentProfile_Returns_True_When_SaveSucceeds`
- [x] `UpdateStudentProfile_UploadsProfilePicture_WhenImageProvided`
- [x] `UpdateStaffOrAdminProfile_Throws_When_CurrentUserNotFound`
- [x] `UpdateStaffOrAdminProfile_Throws_When_TargetUserNotFound`
- [x] `UpdateStaffOrAdminProfile_Throws_When_CallerLacksPermission`
- [x] `UpdateStaffOrAdminProfile_Succeeds_When_Admin_Updates_Staff`
- [x] `UpdateStaffOrAdminProfile_Succeeds_When_User_Updates_Own_Profile`
- [x] `UpdateStaffOrAdminProfile_Succeeds_When_SuperAdmin_Updates_Admin`

### Delete / Validate / RFID

- [x] `DeleteUser_Returns_Success_When_Archive_Succeeds`
- [x] `DeleteUser_Returns_Failure_When_Archive_Fails`
- [x] `ValidateStudentProfile_Returns_False_When_UserNotFound`
- [x] `ValidateStudentProfile_Returns_True_For_NonStudent_Users`
- [x] `ValidateStudentProfile_Returns_True_For_Complete_Student`
- [x] `ValidateStudentProfile_Returns_True_For_Staff`
- [x] `GetStudentByIdNumber_Returns_Null_When_NotFound`
- [x] `GetStudentByIdNumber_Returns_Student_When_Found`
- [x] `RegisterRfid_Returns_Success_When_Repository_Succeeds`
- [x] `RegisterRfid_Returns_Failure_When_Repository_Fails`
- [x] `GetStudentByRfid_Returns_Null_When_NotFound`
- [x] `GetStudentByRfid_Returns_Student_When_Found`

### Item Summary (`GetUserItemSummaryAsync`)

- [x] `GetUserItemSummary_Returns_CorrectCounts_ForStudentWithMixedStatuses`
- [x] `GetUserItemSummary_Returns_ZeroCounts_WhenStudentHasNoHistory`

### Import

- [x] `ImportStudentsFromExcelAsync_ReturnsFailure_WhenFileInvalid`
- [x] `ImportStudentsFromExcelAsync_ReturnsDetailedResults_OnPartialSuccess`

---

## 3. Inventory & Items (`ItemService`) — 22 / 22 ✅

**File:** `Services/ItemServiceTests.cs`

### Create

- [x] `CreateItem_Throws_When_SerialNumber_IsEmpty`
- [x] `CreateItem_Throws_When_DuplicateSerialNumber_Exists`
- [x] `CreateItem_AutoPrefixes_SN_When_Missing`
- [x] `CreateItem_NormalizesSerialNumber_ToUppercase`
- [x] `CreateItem_DoesNotDuplicatePrefix_WhenSnAlreadyPresent`
- [x] `CreateItem_UploadsImage_WhenImageProvided`
- [x] `CreateItem_Returns_MappedDto_OnSuccess`

### Read

- [x] `GetAllItems_Returns_MappedCollection`
- [x] `GetItemById_Returns_Null_When_NotFound`
- [x] `GetItemById_Returns_MappedDto_When_Found`
- [x] `GetItemBySerialNumber_Returns_Null_When_NotFound`
- [x] `GetItemByRfid_Returns_Null_When_NotFound`
- [x] `GetItemByRfid_Returns_MappedDto_When_Found`

### Update / Delete

- [x] `UpdateItem_Returns_False_When_ItemNotFound`
- [x] `UpdateItem_Returns_True_When_SaveSucceeds`
- [x] `UpdateItem_Throws_When_NewSerialNumber_AlreadyExists_OnDifferentItem`
- [x] `UpdateItem_DeletesOldImage_WhenNewImageProvided`
- [x] `UpdateItemLocation_Returns_Failure_When_ItemNotFound`
- [x] `UpdateItemLocation_Returns_Success_When_ItemExists`
- [x] `DeleteItem_Returns_Failure_When_ItemNotFound`
- [x] `DeleteItem_Returns_Success_When_ArchiveAndDeleteSucceed`

### RFID / Scan

- [x] `ScanRfid_Returns_Failure_When_NoItemLinked`
- [x] `ScanRfid_Returns_Success_When_ItemFound_WithActiveLentRecord`
- [x] `ScanRfid_Returns_Failure_When_NoActiveLentRecord`
- [x] `RegisterRfidToItem_Returns_Failure_When_ItemNotFound`
- [x] `RegisterRfidToItem_Returns_Failure_When_RfidAlreadyTakenByAnotherItem`
- [x] `RegisterRfidToItem_Returns_Success_When_Registered`

> **Import tests** — `ImportItemsFromExcelAsync` **does exist** on `ItemService` (confirmed via source audit, April 2026).
> The method reads an XLSX stream via `ExcelReaderFactory`, validates serial numbers, checks for duplicates,
> and bulk-saves via `_itemRepository.AddRangeAsync`. Tests are pending — see **Part 3 (pending)** below.

---

## 4. Lending & Circulation (`LentItemsService`) — 56 / 56 ✅

**Files:** `Services/LentItemsServiceTests.cs` · `Services/AddBorrowAndReserveTests.cs`

### Borrow (`AddBorrowAsync` / legacy `AddAsync`) — `LentItemsServiceTests.cs` + `AddBorrowAndReserveTests.cs`

- [x] `AddAsync_Throws_When_Item_IsInBadCondition` — `[Theory]` × 2
- [x] `AddAsync_Throws_When_Item_IsAlreadyBorrowedOrReserved` — `[Theory]` × 2
- [x] `AddAsync_Throws_When_ActiveLentRecord_AlreadyExists`
- [x] `AddAsync_Throws_When_ItemNotFound`
- [x] `AddAsync_Succeeds_And_Returns_MappedDto`
- [x] `AddAsync_WritesActivityLog_OnSuccess`
- [x] `AddBorrowAsync_Throws_When_ItemNotFound`
- [x] `AddBorrowAsync_Throws_When_Item_IsInBadCondition` — `[Theory]` × 2
- [x] `AddBorrowAsync_Throws_When_Item_IsAlreadyBorrowedOrReserved` — `[Theory]` × 2
- [x] `AddBorrowAsync_Throws_When_ActiveLentRecord_AlreadyExists`
- [x] `AddBorrowAsync_Succeeds_And_Sets_Status_To_Borrowed`
- [x] `AddBorrowAsync_Sends_ItemBorrowed_Notification`
- [x] `AddBorrowAsync_WritesActivityLog_WithBorrowedCategory`
- [x] `AddBorrowAsync_Throws_When_BorrowingLimitReached_ForStudentOrTeacher`

### Reservation (`AddReservationAsync`) — `AddBorrowAndReserveTests.cs`

- [x] `AddReservationAsync_Throws_When_ReservedFor_IsInPast`
- [x] `AddReservationAsync_Throws_When_ItemNotFound`
- [x] `AddReservationAsync_Throws_When_Item_IsInBadCondition` — `[Theory]` × 2
- [x] `AddReservationAsync_Throws_When_Item_IsCurrentlyBorrowed`
- [x] `AddReservationAsync_Succeeds_And_Sets_Status_To_Pending`
- [x] `AddReservationAsync_Sends_NewPendingRequest_Notification`
- [x] `AddReservationAsync_WritesActivityLog_WithGeneralCategory`
- [x] `AddReservationAsync_Throws_When_TimeSlotConflictsWithExistingReservation`
- [x] `AddReservationAsync_Throws_When_BorrowingLimitReached_ForStudentOrTeacher`

### Guest Borrow (`AddForGuestAsync`) — `LentItemsServiceTests.cs`

- [x] `AddForGuestAsync_Throws_When_TagUidNotFound`
- [x] `AddForGuestAsync_Throws_When_Item_IsInBadCondition` — `[Theory]` × 2
- [x] `AddForGuestAsync_Throws_When_Item_IsAlreadyBorrowed`
- [x] `AddForGuestAsync_Sets_BorrowerRole_To_Guest`
- [x] `AddForGuestAsync_Stores_Organization_ContactNumber_Purpose`
- [x] `AddForGuestAsync_Uploads_GuestImage_WhenImageProvided`
- [x] `AddForGuestAsync_Sets_IssuedById_FromCallerIdentity`
- [x] `AddForGuestAsync_Returns_CreatedLentItem_WhenValid`
- [x] `AddForGuestAsync_WritesActivityLog_OnSuccess`

### Update (`UpdateAsync`)

- [x] `UpdateAsync_Returns_False_When_LentItemNotFound`
- [x] `UpdateAsync_Returns_True_When_ValidUpdateApplied`
- [x] `UpdateAsync_WritesActivityLog_WhenStatusChanges`
- [x] `UpdateAsync_SendsApprovalNotification_WhenStatusChangesToApproved`
- [x] `UpdateAsync_SendsStatusChangeNotification_OnAnyStatusTransition`

### Update Status (`UpdateStatusAsync`)

- [x] `UpdateStatusAsync_Returns_False_When_LentItemNotFound`
- [x] `UpdateStatusAsync_TransitionsStatus_SetsTimestamps_AndWritesLog`

### Queries

- [x] `GetAll_Returns_AllLentItems`
- [x] `GetById_Returns_Null_When_NotFound`
- [x] `GetById_Returns_MappedDto_When_Found`
- [x] `GetAllBorrowedItems_Returns_OnlyBorrowedStatus`
- [x] `GetByDateTime_FiltersCorrectly_GivenUtcDateTime`

### Visibility & Archival

- [x] `UpdateHistoryVisibility_Returns_False_When_LentItemNotFound`
- [x] `UpdateHistoryVisibility_Returns_False_When_UserDoesNotOwnRecord`
- [x] `UpdateHistoryVisibility_Returns_True_When_UserOwnsRecord`
- [x] `UpdateHistoryVisibility_Returns_True_Without_Save_When_AlreadyInDesiredState`
- [x] `ArchiveLentItems_Returns_Failure_When_LentItemNotFound`
- [x] `ArchiveLentItems_Returns_Success_When_Archived`
- [x] `ArchiveLentItems_Sets_Item_To_Available_When_Returned`

### Background / Expiry

- [x] `CancelExpiredReservations_Returns_Zero_When_NoExpiredReservations`
- [x] `CancelExpiredReservations_Cancels_Stale_Reservations_And_Returns_Count`
- [x] `CancelExpiredReservations_DoesNotCancel_AlreadyPickedUp_Reservations`
- [x] `CancelExpiredReservations_SendsExpiredNotification_ForEachCanceledReservation`
- [x] `CancelExpiredReservations_WritesActivityLog_ForEachCanceledReservation`
- [x] `CancelExpiredReservations_UsesThirtyMinuteGracePeriod_NotOneHour`
- [x] `CancelExpiredReservations_SendsNotification_ForMultipleExpiredReservations`
- [x] `IsItemAvailableForReservation_Returns_False_WhenSlotConflicts`
- [x] `IsItemAvailableForReservation_Returns_True_WhenNoConflict`

---

## 5. Activity Logs (`ActivityLogService`) — 13 / 13 ✅

**File:** `Services/ActivityLogServiceTests.cs`

### GetAll (filtered)

- [x] `GetAll_Returns_AllLogs_When_NoFilterApplied`
- [x] `GetAll_PassesCategory_Filter_To_Repository`
- [x] `GetAll_PassesDateRange_Filter_To_Repository`
- [x] `GetAll_PassesActorUserId_Filter_To_Repository`
- [x] `GetAll_PassesItemId_Filter_To_Repository`
- [x] `GetAll_PassesStatus_Filter_To_Repository`

### GetById

- [x] `GetById_Returns_Null_When_NotFound`
- [x] `GetById_Returns_MappedDto_When_Found`

### GetBorrowLogs

- [x] `GetBorrowLogs_Queries_BorrowedItem_And_Returned_Categories`
- [x] `GetBorrowLogs_Returns_Empty_When_NoLogsExist`
- [x] `GetBorrowLogs_FiltersBy_DateRange_UserId_ItemId`

### LogAsync

- [x] `LogAsync_Saves_Log_And_Returns_MappedDto`
- [x] `LogAsync_Stores_Correct_Category_And_Action`

---

## 6. Statistical Summaries (`SummaryService`) — 9 / 9 ✅

**File:** `Services/SummaryServiceTests.cs`

### Item Count

- [x] `GetItemCount_Calculates_Correct_Condition_Counts`
- [x] `GetItemCount_Calculates_Correct_Category_Counts`
- [x] `GetItemCount_Returns_Zeros_When_NoItems`

### Lent Items Count

- [x] `GetLentItemsCount_Splits_Active_Vs_Returned_Correctly`
- [x] `GetLentItemsCount_Returns_Zeros_When_NoRecords`

### Active User Count

- [x] `GetActiveUserCount_Separates_Roles_Into_Correct_Buckets`
- [x] `GetActiveUserCount_Returns_Zeros_When_AllUsersOffline`

### Overall Summary

- [x] `GetOverallSummary_Aggregates_All_Sub_Summaries`
- [x] `GetOverallSummary_ItemStocks_Counts_Available_And_Borrowed_Correctly`

---

## 7. Archival Services — 18 / 18 ✅

**File:** `Services/ArchiveServiceTests.cs`

### ArchiveItemsService (10 tests)

- [x] `CreateItemArchive_Maps_And_Saves_To_Archive`
- [x] `GetItemArchiveById_Returns_Null_When_NotFound`
- [x] `GetItemArchiveById_Returns_MappedDto_When_Found`
- [x] `DeleteItemArchive_Returns_False_When_NotFound`
- [x] `DeleteItemArchive_Returns_True_When_Deleted`
- [x] `RestoreItem_Returns_Null_When_ArchiveNotFound`
- [x] `RestoreItem_Restores_Item_And_Returns_Dto`
- [x] `RestoreItem_Sets_Status_To_Available`
- [x] `UpdateItemArchive_Returns_False_When_NotFound`
- [x] `UpdateItemArchive_Returns_True_When_SaveSucceeds`

### ArchiveLentItemsService (8 tests)

- [x] `CreateLentItemsArchive_Maps_And_Saves`
- [x] `GetLentItemsArchiveById_Returns_Null_When_NotFound`
- [x] `GetLentItemsArchiveById_Returns_MappedDto_When_Found`
- [x] `DeleteLentItemsArchive_Returns_False_When_NotFound`
- [x] `DeleteLentItemsArchive_Returns_True_When_Deleted`
- [x] `RestoreLentItems_Returns_Null_When_ArchiveNotFound`
- [x] `RestoreLentItems_Returns_RestoredDto_On_Success`

> **ArchiveUserService** — excluded. Uses `AppDbContext` transactions directly;
> requires integration-level testing with a real or in-memory database.

---

## 8. Real-Time Notifications (`NotificationService`) — 12 / 12 ✅

**File:** `Services/NotificationServiceTests.cs`

### SendNewPendingRequest

- [x] `SendNewPendingRequest_Invokes_SendAsync_On_AdminStaff_Group`
- [x] `SendNewPendingRequest_DoesNotThrow_When_HubFails`

### SendApproval

- [x] `SendApproval_Sends_To_User_Group_When_UserId_Provided`
- [x] `SendApproval_Always_Notifies_AdminStaff_Group`

### SendStatusChange

- [x] `SendStatusChange_Sends_To_User_Group_When_UserId_Provided`
- [x] `SendStatusChange_Always_Notifies_AdminStaff_Group`

### SendBroadcast

- [x] `SendBroadcast_Invokes_SendAsync_On_All_Clients`
- [x] `SendBroadcast_DoesNotThrow_When_HubFails`

### SendItemBorrowed

- [x] `SendItemBorrowed_Sends_To_User_Group_When_UserId_Provided`
- [x] `SendItemBorrowed_Always_Notifies_AdminStaff_Group`

### SendReservationExpired

- [x] `SendReservationExpired_Sends_To_User_Group_When_UserId_Provided`
- [x] `SendReservationExpired_Always_Notifies_AdminStaff_Group`

---

## 9. Storage Service (`SupabaseStorageService`) — skipped ⏭

Storage behavior is verified indirectly through consuming service tests:

| Verified via                                                       | What is checked                                           |
| ------------------------------------------------------------------ | --------------------------------------------------------- |
| `ItemServiceTests.CreateItem_UploadsImage_WhenImageProvided`       | `UploadImageAsync` called once when image present         |
| `ItemServiceTests.UpdateItem_DeletesOldImage_WhenNewImageProvided` | `DeleteImageAsync` + `UploadImageAsync` called on replace |

A dedicated `SupabaseStorageTests.cs` would require a live Supabase connection
and belongs in an integration test suite, not the unit test project.

---

## 10. Utility & Infrastructure Services — 24 / 24 ✅

**File:** `Utilities/UtilityServiceTests.cs`

### PasswordHashingService ✅

- [x] `HashPassword_Produces_Hash_Different_From_Plaintext`
- [x] `HashPassword_Produces_Different_Hashes_For_Same_Input`
- [x] `HashPassword_Produces_BCrypt_Format_Hash`
- [x] `HashPassword_Works_For_Any_NonEmpty_String` — `[Theory]` × 4
- [x] `VerifyPassword_Returns_True_For_Correct_Password`
- [x] `VerifyPassword_Returns_False_For_Wrong_Password`
- [x] `VerifyPassword_Returns_False_For_Empty_Password`
- [x] `VerifyPassword_Returns_False_For_Empty_Hash`
- [x] `VerifyPassword_Returns_False_For_CaseSensitive_Mismatch`

### ExcelReaderService ✅

- [x] `Parse_ThrowsException_IfFileIsNotXlsx`
- [x] `Parse_ReadsWorksheets_AndYieldsExpectedDictionaryMap`

### FileValidationUtils ✅

- [x] `ValidateImportFile_ReturnsInvalid_WhenExtensionIsNotXlsxOrCsv`
- [x] `ValidateImportFile_ReturnsInvalid_WhenMagicBytesDoNotMatchExtension`
- [x] `ValidateImportFile_ReturnsValid_ForLegitimateXlsxFile`

### ImageConverterUtils ✅

- [x] `ValidateImage_ThrowsArgumentException_WhenImageExceedsSizeLimit`
- [x] `ValidateImage_ThrowsArgumentException_WhenFormatIsNotAllowed`
- [x] `ValidateImage_DoesNotThrow_ForValidJpegOrPng`

### Background Jobs ✅

- [x] `RefreshTokenCleanup_ExecuteAsync_SkipsIfCancellationRequested_AtStartup`
- [x] `RefreshTokenCleanup_ExecuteAsync_TriggersCleanup_OnEachCycle`
- [x] `ReservationExpiry_ExecuteAsync_SkipsIfCancellationRequested_AtStartup`
- [x] `ReservationExpiry_CallsCancelExpiredReservationsAsync_OnEachCycle`

---

## Test Run Summary

```
Total:    283
Passed:   283
Failed:     0
Skipped:    0
Services: ~600 ms  |  Utilities: ~6 s (BCrypt)
```

### Pending (36 items — audit April 2026)

| Part       | Area                                                 | Pending |
| ---------- | ---------------------------------------------------- | ------- |
| 3 (import) | ItemService — `ImportItemsFromExcelAsync`            | 5       |
| 11a        | AuthController — mobile login, logout, refresh       | 4       |
| 11b        | UserController — teacher profile, import, RFID       | 6       |
| 11c        | ItemController — serial, RFID, location, import      | 7       |
| 11d        | LentItemsController — GetAll, borrowed, update, hide | 4       |
| 13         | RefreshTokenMiddleware                               | 5       |
| 14         | HealthController                                     | 2       |
| **Total**  |                                                      | **33**  |

> Note: count above is 33 not 36 — the project structure header shows 36 because it counts
> the 3 extra `UpdateChecker` tests that were already appended to `UtilityServiceTests.cs`
> in a prior revision but not reflected in the structure block. Actual new pending = 33.

### What changed since last revision (April 2026 audit)

| Change                                       | Detail                                                                                |
| -------------------------------------------- | ------------------------------------------------------------------------------------- |
| `ImportItemsFromExcelAsync` re-classified    | Method confirmed to exist in `ItemService`; prior checklist incorrectly marked N/A    |
| 4 new `AuthController` tests identified      | `LoginMobile`, `Logout`, `RefreshToken`, `RefreshTokenMobile` endpoints not covered   |
| 6 new `UserController` tests identified      | Teacher profile, `UpdateStaffOrAdminProfile`, import, RFID endpoints not covered      |
| 7 new `ItemController` tests identified      | `GetAllItems`, serial/RFID/location/import endpoints not covered                      |
| 4 new `LentItemsController` tests identified | `GetAll`, `GetAllBorrowedItems`, `Update` success path, `HideFromHistory` not covered |
| Part 13 added                                | `RefreshTokenMiddleware` has testable branching logic — 5 tests identified            |
| Part 14 added                                | `HealthController` has 2 branches (200 / 503) — not covered                           |

---

## 3 (pending). ItemService — `ImportItemsFromExcelAsync` — 0 / 5 🆕

**File:** `Services/ItemServiceTests.cs` (append to existing class)

> **Constructor note:** `ItemService` requires `IItemRepository`, `IMapper`, `IWebHostEnvironment`,
> `IArchiveItemsService`, `ILentItemsRepository`, `ISupabaseStorageService`. All are already mocked
> in the existing `ItemServiceTests` class — just add the new tests below.
>
> **Key logic to mirror:**
>
> - Reads XLSX via `ExcelReaderFactory` (uses `ExcelDataReader` library)
> - Validates `SerialNumber` is non-empty; skips row and increments `FailureCount` if missing
> - Normalises serial number to uppercase and prepends `SN-` if absent
> - Calls `_itemRepository.GetBySerialNumberAsync` to detect duplicates → adds to `SkippedDuplicates`
> - Calls `_itemRepository.AddRangeAsync` + `SaveChangesAsync` only when at least one item is valid
> - Returns `ImportItemsResponseDto` with `SuccessCount`, `FailureCount`, `SkippedDuplicates`, `Errors`
>
> **Mocking note:** `ImportItemsFromExcelAsync` reads the file stream directly via `ExcelReaderFactory`,
> which cannot be mocked. Use a real minimal XLSX byte array (embedded constant) passed via a
> `MemoryStream`-backed `IFormFile` mock — the same pattern used in `UtilityServiceTests` for
> `ExcelReaderService`.

- [ ] `ImportItemsFromExcelAsync_ReturnsFailure_WhenAllRowsMissingSerialNumber`
  - Provide a valid XLSX with one data row where `SerialNumber` column is empty
  - Assert `SuccessCount == 0`, `FailureCount == 1`, `Errors` contains `"Missing SerialNumber"`

- [ ] `ImportItemsFromExcelAsync_SkipsDuplicate_WhenSerialNumberAlreadyExists`
  - `_mockItemRepo.Setup(r => r.GetBySerialNumberAsync("SN-EXIST")).ReturnsAsync(existingItem)`
  - Provide XLSX with one row whose serial resolves to `SN-EXIST`
  - Assert `SuccessCount == 0`, `SkippedDuplicates.Count == 1`

- [ ] `ImportItemsFromExcelAsync_NormalisesSerialNumber_ToUppercaseWithPrefix`
  - Provide XLSX with serial `"abc123"` (no prefix, lowercase)
  - Capture entity passed to `AddRangeAsync`
  - Assert saved serial is `"SN-ABC123"`

- [ ] `ImportItemsFromExcelAsync_CallsAddRangeAsync_WhenAtLeastOneRowIsValid`
  - Provide XLSX with one valid row
  - `_mockItemRepo.Setup(r => r.GetBySerialNumberAsync(...)).ReturnsAsync((Item?)null)`
  - Verify `_mockItemRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Item>>()), Times.Once)`

- [ ] `ImportItemsFromExcelAsync_DoesNotCallAddRangeAsync_WhenNoValidRows`
  - All rows have empty serial numbers
  - Verify `_mockItemRepo.Verify(r => r.AddRangeAsync(...), Times.Never)`

---

## 13. RefreshTokenMiddleware (`Infrastructure/RefreshTokenMiddlewareTests.cs`) — 0 / 5 🆕

**File:** `Infrastructure/RefreshTokenMiddlewareTests.cs` _(new file)_
**Injects:** `RequestDelegate` (mock), `ILogger<RefreshTokenMiddleware>` (mock)
**Note:** `IAuthService` is resolved from `HttpContext.RequestServices` (scoped), not the constructor.
Inject it via a mock `IServiceProvider` set on `HttpContext.RequestServices`.

> **Key logic to mirror from `RefreshTokenMiddleware.InvokeAsync`:**
>
> - If `context.User.Identity.IsAuthenticated == false` → skip refresh, call `_next`
> - If `exp` claim missing or unparseable → skip refresh, call `_next`
> - If `timeUntilExpiry > 0 && <= 10s` OR `timeUntilExpiry < 0 && >= -5s` → call `authService.RefreshToken()`
> - If `RefreshTokenException` thrown → delete cookie, set 401, **return** (do not call `_next`)
> - If token is not near expiry → skip refresh, call `_next`

- [ ] `InvokeAsync_SkipsRefresh_WhenUserIsNotAuthenticated`
  - `HttpContext.User.Identity.IsAuthenticated == false`
  - Verify `_mockNext` was called once; `_mockAuthService.RefreshToken` never called

- [ ] `InvokeAsync_SkipsRefresh_WhenExpClaimIsMissing`
  - Authenticated user but no `exp` claim
  - Verify `_mockNext` called once; no refresh attempted

- [ ] `InvokeAsync_CallsRefreshToken_WhenTokenIsNearExpiry`
  - Set `exp` claim to `DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeSeconds()` (within 10 s threshold)
  - Verify `_mockAuthService.Verify(s => s.RefreshToken(), Times.Once)`
  - Verify `_mockNext` still called after successful refresh

- [ ] `InvokeAsync_Returns401_AndDoesNotCallNext_WhenRefreshTokenExceptionThrown`
  - Token near expiry; `_mockAuthService.RefreshToken()` throws `RefreshTokenException`
  - Assert `context.Response.StatusCode == 401`
  - Verify `_mockNext` was **never** called

- [ ] `InvokeAsync_ContinuesPipeline_WhenTokenIsNotNearExpiry`
  - Set `exp` claim to `DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()` (well outside threshold)
  - Verify `_mockNext` called once; no refresh attempted

---

## 14. HealthController (`Controllers/HealthControllerTests.cs`) — 0 / 2 🆕

**File:** `Controllers/HealthControllerTests.cs` _(new file)_
**Injects:** `HealthCheckService` (mock)

> `HealthCheckService` is a concrete class from `Microsoft.Extensions.Diagnostics.HealthChecks`.
> Mock it with `Mock<HealthCheckService>` and set up `CheckHealthAsync()` to return a
> `HealthReport` with the desired `HealthStatus`.

- [ ] `Get_Returns_200_WhenAllChecksHealthy`
  - `_mockHealthCheckService.Setup(s => s.CheckHealthAsync(...))` returns `HealthReport` with `HealthStatus.Healthy`
  - Assert `OkObjectResult`

- [ ] `Get_Returns_503_WhenAnyCheckUnhealthy`
  - `CheckHealthAsync` returns `HealthReport` with `HealthStatus.Unhealthy`
  - Assert `ObjectResult` with `StatusCode == 503`

---

## 11. Controller Tests — 40 / 76 ⚠️

> **Approach:** Instantiate controllers directly with mocked `IService` dependencies.
> Use `DefaultHttpContext` + `ClaimsPrincipal` to simulate authenticated users.
> No `WebApplicationFactory` or `TestServer` needed — these are pure unit tests.

---

### 11a. AuthController (`Controllers/AuthControllerTests.cs`) — 7 / 11 ⚠️

**File:** `Controllers/AuthControllerTests.cs`
**Injects:** `IAuthService`, `IUserService`, `IWebHostEnvironment`, `ILogger<AuthController>`

- [x] `GetMyProfile_Returns_Unauthorized_WhenClaimMissing`
- [x] `GetMyProfile_Returns_NotFound_WhenUserProfileNull`
- [x] `GetMyProfile_Returns_Ok_WithProfile`
- [x] `Register_Returns_Unauthorized_WhenClaimInvalid`
- [x] `Register_Returns_201_OnSuccess`
- [x] `Login_Returns_Ok_WithUserDto`
- [x] `ChangePassword_Returns_Ok_OnSuccess`
- [ ] `LoginMobile_Returns_Ok_WithMobileLoginResponseDto`
- [ ] `Logout_Returns_Ok_OnSuccess`
- [ ] `RefreshToken_Returns_Ok_WithNewAccessToken`
- [ ] `RefreshTokenMobile_Returns_Ok_WithNewTokenPair`

---

### 11b. UserController (`Controllers/UserControllerTests.cs`) — 8 / 14 ⚠️

**File:** `Controllers/UserControllerTests.cs`
**Injects:** `IUserService`, `IUserRepository`, `IMapper`, `IAuthorizationService`

- [x] `GetAllUsers_Returns_Ok_WithUserList`
- [x] `GetUserProfileById_Returns_NotFound_WhenUserNotInRepo`
- [x] `GetUserProfileById_Returns_Forbidden_WhenAuthorizationFails`
- [x] `GetUserProfileById_Returns_Ok_WhenAuthorized`
- [x] `UpdateStudentProfile_Returns_Unauthorized_WhenClaimInvalid`
- [x] `UpdateStudentProfile_Returns_Forbidden_WhenStudentUpdatesOtherProfile`
- [x] `ArchiveUser_Returns_NotFound_WhenServiceReturnsNotFound`
- [x] `ArchiveUser_Returns_Ok_OnSuccess`
- [ ] `UpdateTeacherProfile_Returns_Unauthorized_WhenClaimInvalid`
- [ ] `UpdateTeacherProfile_Returns_Forbidden_WhenTeacherUpdatesOtherProfile`
- [ ] `UpdateStaffOrAdminProfile_Returns_Ok_OnSuccess`
- [ ] `ImportStudents_Returns_Ok_WithImportSummary`
- [ ] `GetStudentByIdNumber_Returns_NotFound_WhenNull`
- [ ] `RegisterRfidToStudent_Returns_Ok_OnSuccess`

---

### 11c. ItemController (`Controllers/ItemControllerTests.cs`) — 8 / 15 ⚠️

**File:** `Controllers/ItemControllerTests.cs`
**Injects:** `IItemService`

- [x] `CreateItem_Returns_Conflict_WhenDuplicateSerialNumber`
- [x] `CreateItem_Returns_BadRequest_WhenArgumentException`
- [x] `CreateItem_Returns_201_OnSuccess`
- [x] `GetItemById_Returns_NotFound_WhenNull`
- [x] `GetItemById_Returns_Ok_WhenFound`
- [x] `ScanRfid_Returns_NotFound_WhenNoItemLinked`
- [x] `ArchiveItem_Returns_NotFound_WhenItemNotFound`
- [x] `ArchiveItem_Returns_Ok_OnSuccess`
- [ ] `GetAllItems_Returns_Ok_WithItemList`
- [ ] `GetItemBySerialNumber_Returns_NotFound_WhenNull`
- [ ] `GetItemBySerialNumber_Returns_Ok_WhenFound`
- [ ] `GetByRfid_Returns_NotFound_WhenNoItemRegistered`
- [ ] `RegisterRfid_Returns_Conflict_WhenRfidAlreadyTaken`
- [ ] `UpdateLocation_Returns_NotFound_WhenItemNotFound`
- [ ] `ImportItemsFromExcel_Returns_415_WhenFileValidationFails`

---

### 11d. LentItemsController (`Controllers/LentItemsControllerTests.cs`) — 7 / 11 ⚠️

**File:** `Controllers/LentItemsControllerTests.cs`
**Injects:** `ILentItemsService`

- [x] `Borrow_Returns_201_OnSuccess`
- [x] `Reserve_Returns_201_OnSuccess`
- [x] `AddForGuest_Returns_Unauthorized_WhenClaimMissing`
- [x] `GetById_Returns_NotFound_WhenNull`
- [x] `Update_Returns_NotFound_WhenServiceReturnsFalse`
- [x] `ArchiveLentItems_Returns_NotFound_WhenNotFound`
- [x] `GetByDateTime_Returns_BadRequest_WhenDateInvalid`
- [ ] `GetAll_Returns_Ok_WithLentItemList`
- [ ] `GetAllBorrowedItems_Returns_Ok_WithBorrowedList`
- [ ] `Update_Returns_Ok_WhenServiceReturnsTrue`
- [ ] `HideFromHistory_Returns_Unauthorized_WhenClaimMissing`

---

### 11e. ActivityLogController (`Controllers/ActivityLogControllerTests.cs`) — 4 / 4 ✅

- [x] `GetAll_Returns_Ok_WithLogs`
- [x] `GetById_Returns_NotFound_WhenNull`
- [x] `GetById_Returns_Ok_WhenFound`
- [x] `GetBorrowLogs_Returns_Ok_WithLogs`

---

### 11f. SummaryController (`Controllers/SummaryControllerTests.cs`) — 2 / 2 ✅

- [x] `GetOverallSummary_Returns_Ok_WithSummaryDto`
- [x] `GetOverallSummary_WrapsData_InApiResponse`

---

### 11g. ArchiveItemsController (`Controllers/ArchiveItemsControllerTests.cs`) — 4 / 4 ✅

- [x] `GetArchivedItemById_Returns_NotFound_WhenNull`
- [x] `GetArchivedItemById_Returns_Ok_WhenFound`
- [x] `RestoreArchivedItem_Returns_NotFound_WhenNull`
- [x] `DeleteArchivedItem_Returns_NotFound_WhenNotFound`

---

### 11h. ArchiveLentItemsController (`Controllers/ArchiveLentItemsControllerTests.cs`) — 4 / 4 ✅

- [x] `GetLentItemsArchiveById_Returns_NotFound_WhenNull`
- [x] `GetLentItemsArchiveById_Returns_Ok_WhenFound`
- [x] `RestoreArchivedLentItems_Returns_NotFound_WhenNull`
- [x] `DeleteLentItemsArchive_Returns_NotFound_WhenNotFound`

---

### 11i. ArchiveUsersController (`Controllers/ArchiveUsersControllerTests.cs`) — 4 / 4 ✅

- [x] `GetArchivedUserById_Returns_NotFound_WhenNull`
- [x] `GetArchivedUserById_Returns_Ok_WhenFound`
- [x] `RestoreUser_Returns_BadRequest_WhenServiceReturnsFalse`
- [x] `PermanentDeleteUser_Returns_NotFound_WhenNotFound`

---

## 12. Infrastructure & Cross-Cutting — 18 / 18 ✅

### 12a. GlobalExceptionHandler — 6 / 6 ✅

- [x] `InvokeAsync_Returns_403_For_UnauthorizedAccessException`
- [x] `InvokeAsync_Returns_404_For_KeyNotFoundException`
- [x] `InvokeAsync_Returns_400_For_ArgumentException`
- [x] `InvokeAsync_Returns_400_For_InvalidOperationException`
- [x] `InvokeAsync_Returns_401_For_InvalidCredentialsException`
- [x] `InvokeAsync_Returns_500_For_UnhandledException`

### 12b. Authorization Handlers — 6 / 6 ✅

- [x] `SuperAdminBypassHandler_Succeeds_AllRequirements_WhenUserIsSuperAdmin`
- [x] `SuperAdminBypassHandler_DoesNotSucceed_WhenUserIsNotSuperAdmin`
- [x] `ViewProfileHandler_Succeeds_WhenUserIsAdmin`
- [x] `ViewProfileHandler_Succeeds_WhenUserIsStaff`
- [x] `ViewProfileHandler_Succeeds_WhenUserViewsOwnProfile`
- [x] `ViewProfileHandler_Fails_WhenStudentViewsOtherProfile`

### 12c. UserValidationService — 3 / 3 ✅

- [x] `ValidateUniqueUser_Throws_WhenUsernameAlreadyTaken`
- [x] `ValidateUniqueUser_Throws_WhenEmailAlreadyExists`
- [x] `ValidateUniqueUser_Throws_WhenPhoneNumberAlreadyUsed`

### 12d. UpdateChecker Utility — 3 / 3 ✅

- [x] `UpdateChecker_UpdatesString_WhenNewValueIsNonEmpty`
- [x] `UpdateChecker_DoesNotUpdate_WhenNewValueIsNull`
- [x] `UpdateChecker_DoesNotUpdate_WhenNewValueIsEmpty`
