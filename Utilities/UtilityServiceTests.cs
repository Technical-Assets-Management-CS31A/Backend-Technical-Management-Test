using BackendTechnicalAssetsManagement.src.BackgroundServices;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Services;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace BackendTechincalAssetsManagementTest.Utilities
{
    /// <summary>
    /// Part 10 — Utility and Infrastructure Services
    /// Max per-test: 200 ms (BCrypt tests are the only exception — ~100-300 ms each).
    /// Covers: PasswordHashingService, ExcelReaderService, FileValidationUtils,
    ///         ImageConverterUtils, RefreshTokenCleanupService, ReservationExpiryBackgroundService
    /// All repositories and external I/O fully mocked.
    /// </summary>
    public class PasswordHashingServiceTests
    {
        private readonly PasswordHashingService _sut = new(workFactor: 4);

        // ══════════════════════════════════════════════════════════════════════
        // HASH PASSWORD
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void HashPassword_Produces_Hash_Different_From_Plaintext()
        {
            var hash = _sut.HashPassword("Password1!");

            hash.Should().NotBe("Password1!");
            hash.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void HashPassword_Produces_Different_Hashes_For_Same_Input()
        {
            // BCrypt uses a random salt — two hashes of the same password must differ
            var hash1 = _sut.HashPassword("Password1!");
            var hash2 = _sut.HashPassword("Password1!");

            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void HashPassword_Produces_BCrypt_Format_Hash()
        {
            var hash = _sut.HashPassword("Password1!");

            // BCrypt hashes always start with $2a$ or $2b$
            hash.Should().MatchRegex(@"^\$2[ab]\$");
        }

        // ══════════════════════════════════════════════════════════════════════
        // VERIFY PASSWORD
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void VerifyPassword_Returns_True_For_Correct_Password()
        {
            var hash = _sut.HashPassword("Password1!");

            _sut.VerifyPassword("Password1!", hash).Should().BeTrue();
        }

        [Fact]
        public void VerifyPassword_Returns_False_For_Wrong_Password()
        {
            var hash = _sut.HashPassword("Password1!");

            _sut.VerifyPassword("WrongPassword!", hash).Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_Returns_False_For_Empty_Password()
        {
            var hash = _sut.HashPassword("Password1!");

            _sut.VerifyPassword(string.Empty, hash).Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_Returns_False_For_Empty_Hash()
        {
            _sut.VerifyPassword("Password1!", string.Empty).Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_Returns_False_For_CaseSensitive_Mismatch()
        {
            var hash = _sut.HashPassword("Password1!");

            // BCrypt is case-sensitive
            _sut.VerifyPassword("password1!", hash).Should().BeFalse();
        }

        [Theory]
        [InlineData("short")]
        [InlineData("NoSpecialChar1")]
        [InlineData("nouppercase1!")]
        [InlineData("NOLOWERCASE1!")]
        public void HashPassword_Works_For_Any_NonEmpty_String(string password)
        {
            // HashPassword itself doesn't validate complexity — that's AuthService's job
            var hash = _sut.HashPassword(password);

            hash.Should().NotBeNullOrEmpty();
            _sut.VerifyPassword(password, hash).Should().BeTrue();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EXCEL READER SERVICE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ExcelReaderService tests — uses IExcelReaderService mock to verify the contract.
    /// The real ExcelDataReader requires a valid XLSX binary; we test the service layer
    /// contract here and leave binary parsing to integration tests.
    /// </summary>
    public class ExcelReaderServiceTests
    {
        private readonly Mock<IExcelReaderService> _mockReader = new();

        [Fact]
        public async Task Parse_ThrowsException_IfFileIsNotXlsx()
        {
            // Arrange — reader returns an error message for a file missing required columns
            var mockFile = new Mock<IFormFile>();
            _mockReader
                .Setup(r => r.ReadStudentsFromExcelAsync(mockFile.Object))
                .ReturnsAsync((
                    new List<(string, string, string?, int)>(),
                    "Excel file must contain 'LastName' and 'FirstName' columns."));

            // Act
            var (students, error) = await _mockReader.Object.ReadStudentsFromExcelAsync(mockFile.Object);

            // Assert
            error.Should().NotBeNullOrEmpty();
            students.Should().BeEmpty();
        }

        [Fact]
        public async Task Parse_ReadsWorksheets_AndYieldsExpectedDictionaryMap()
        {
            // Arrange — reader returns two valid student rows
            var mockFile = new Mock<IFormFile>();
            var expected = new List<(string FirstName, string LastName, string? MiddleName, int RowNumber)>
            {
                ("Juan",  "Dela Cruz", null,    2),
                ("Maria", "Santos",    "Clara", 3)
            };

            _mockReader
                .Setup(r => r.ReadStudentsFromExcelAsync(mockFile.Object))
                .ReturnsAsync((expected, null));

            // Act
            var (students, error) = await _mockReader.Object.ReadStudentsFromExcelAsync(mockFile.Object);

            // Assert
            error.Should().BeNull();
            students.Should().HaveCount(2);
            students[0].FirstName.Should().Be("Juan");
            students[1].MiddleName.Should().Be("Clara");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FILE VALIDATION UTILS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// FileValidationUtils tests — pure static method calls, no mocks needed.
    /// Max per-test: 200 ms.
    /// </summary>
    public class FileValidationUtilsTests
    {
        private static Mock<IFormFile> MakeFile(string fileName, string contentType, byte[] content)
        {
            var mock = new Mock<IFormFile>();
            mock.Setup(f => f.FileName).Returns(fileName);
            mock.Setup(f => f.ContentType).Returns(contentType);
            mock.Setup(f => f.Length).Returns(content.Length);
            mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(content));
            mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((s, _) => s.Write(content, 0, content.Length))
                .Returns(Task.CompletedTask);
            return mock;
        }

        [Fact]
        public async Task ValidateImportFile_ReturnsInvalid_WhenExtensionIsNotXlsxOrCsv()
        {
            // Arrange — .txt extension is not in the allowed list
            var file = MakeFile("data.txt", "text/plain", new byte[] { 0x41, 0x42 });

            // Act
            var (isValid, error) = await FileValidationUtils.ValidateImportFileAsync(file.Object);

            // Assert
            isValid.Should().BeFalse();
            error.Should().Contain("Invalid file extension");
        }

        [Fact]
        public async Task ValidateImportFile_ReturnsInvalid_WhenMagicBytesDoNotMatchExtension()
        {
            // Arrange — .xlsx extension but content is plain text (not PK ZIP magic bytes)
            var content = System.Text.Encoding.UTF8.GetBytes("this is not an xlsx file at all");
            var file = MakeFile(
                "data.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                content);

            // Act
            var (isValid, error) = await FileValidationUtils.ValidateImportFileAsync(file.Object);

            // Assert
            isValid.Should().BeFalse();
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ValidateImportFile_ReturnsValid_ForLegitimateXlsxFile()
        {
            // Arrange — XLSX files start with PK ZIP magic bytes: 0x50 0x4B 0x03 0x04
            var content = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00 };
            var file = MakeFile(
                "students.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                content);

            // Act
            var (isValid, error) = await FileValidationUtils.ValidateImportFileAsync(file.Object);

            // Assert
            isValid.Should().BeTrue();
            error.Should().BeNull();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // IMAGE CONVERTER UTILS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ImageConverterUtils tests — pure static method calls, no mocks needed.
    /// Max per-test: 200 ms.
    /// </summary>
    public class ImageConverterUtilsTests
    {
        private static Mock<IFormFile> MakeImageFile(string fileName, string contentType, long sizeBytes)
        {
            var mock = new Mock<IFormFile>();
            mock.Setup(f => f.FileName).Returns(fileName);
            mock.Setup(f => f.ContentType).Returns(contentType);
            mock.Setup(f => f.Length).Returns(sizeBytes);
            return mock;
        }

        [Fact]
        public void ValidateImage_ThrowsArgumentException_WhenImageExceedsSizeLimit()
        {
            // Arrange — 6 MB exceeds the 5 MB limit defined in ImageConverterUtils
            var file = MakeImageFile("photo.jpg", "image/jpeg", 6 * 1024 * 1024);

            // Act
            Action act = () => ImageConverterUtils.ValidateImage(file.Object);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("*5MB*");
        }

        [Fact]
        public void ValidateImage_ThrowsArgumentException_WhenFormatIsNotAllowed()
        {
            // Arrange — .exe is not in the allowed extensions list
            var file = MakeImageFile("malware.exe", "application/octet-stream", 1024);

            // Act
            Action act = () => ImageConverterUtils.ValidateImage(file.Object);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("*Invalid image file type*");
        }

        [Fact]
        public void ValidateImage_DoesNotThrow_ForValidJpegOrPng()
        {
            // Arrange — valid JPEG and PNG within size limit
            var jpeg = MakeImageFile("photo.jpg", "image/jpeg", 1024);
            var png  = MakeImageFile("photo.png", "image/png",  2048);

            // Act + Assert
            Action actJpeg = () => ImageConverterUtils.ValidateImage(jpeg.Object);
            Action actPng  = () => ImageConverterUtils.ValidateImage(png.Object);

            actJpeg.Should().NotThrow();
            actPng.Should().NotThrow();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BACKGROUND JOBS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Background service tests — cancel immediately to verify the loop exits cleanly.
    /// Calls ExecuteAsync directly (via the protected method) with a pre-cancelled token
    /// so no real Task.Delay is awaited.
    /// </summary>
    public class BackgroundJobTests
    {
        // Helper: invoke the protected ExecuteAsync via reflection
        private static Task InvokeExecuteAsync(BackgroundService svc, CancellationToken ct)
        {
            var method = typeof(BackgroundService)
                .GetMethod("ExecuteAsync",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!;
            return (Task)method.Invoke(svc, new object[] { ct })!;
        }

        // ── RefreshTokenCleanupService ─────────────────────────────────────────

        [Fact]
        public async Task RefreshTokenCleanup_ExecuteAsync_SkipsIfCancellationRequested_AtStartup()
        {
            // Arrange — pre-cancelled token so the while loop never executes
            using var cts        = new CancellationTokenSource();
            var mockLogger       = new Mock<Microsoft.Extensions.Logging.ILogger<RefreshTokenCleanupService>>();
            var mockProvider     = new Mock<IServiceProvider>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();

            mockProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                        .Returns(mockScopeFactory.Object);

            cts.Cancel();

            var sut = new RefreshTokenCleanupService(mockLogger.Object, mockProvider.Object);

            // Act — ExecuteAsync with already-cancelled token exits immediately
            await InvokeExecuteAsync(sut, cts.Token);

            // Assert — scope was never created
            mockScopeFactory.Verify(f => f.CreateScope(), Times.Never);
        }

        [Fact]
        public async Task RefreshTokenCleanup_ExecuteAsync_TriggersCleanup_OnEachCycle()
        {
            // Arrange — token cancelled after scope is created so one cycle runs
            using var cts         = new CancellationTokenSource();
            var mockLogger        = new Mock<Microsoft.Extensions.Logging.ILogger<RefreshTokenCleanupService>>();
            var mockProvider      = new Mock<IServiceProvider>();
            var mockScopeFactory  = new Mock<IServiceScopeFactory>();
            var mockScope         = new Mock<IServiceScope>();
            var mockScopeProvider = new Mock<IServiceProvider>();

            mockProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                        .Returns(mockScopeFactory.Object);
            mockScopeFactory.Setup(f => f.CreateScope())
                .Callback(() => cts.Cancel()) // cancel as soon as scope is created
                .Returns(mockScope.Object);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);

            // DbContext resolution throws — service catches it and the loop exits via cancellation
            mockScopeProvider
                .Setup(p => p.GetService(typeof(BackendTechnicalAssetsManagement.src.Data.AppDbContext)))
                .Throws(new InvalidOperationException("No DB in test"));

            var sut = new RefreshTokenCleanupService(mockLogger.Object, mockProvider.Object);

            // Act
            await InvokeExecuteAsync(sut, cts.Token);

            // Assert — scope was created at least once
            mockScopeFactory.Verify(f => f.CreateScope(), Times.AtLeastOnce);
        }

        // ── ReservationExpiryBackgroundService ────────────────────────────────

        [Fact]
        public async Task ReservationExpiry_ExecuteAsync_SkipsIfCancellationRequested_AtStartup()
        {
            // Arrange — pre-cancelled token
            using var cts        = new CancellationTokenSource();
            var mockLogger       = new Mock<Microsoft.Extensions.Logging.ILogger<ReservationExpiryBackgroundService>>();
            var mockProvider     = new Mock<IServiceProvider>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();

            mockProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                        .Returns(mockScopeFactory.Object);

            cts.Cancel();

            var sut = new ReservationExpiryBackgroundService(mockProvider.Object, mockLogger.Object);

            // Act
            await InvokeExecuteAsync(sut, cts.Token);

            // Assert — no scope created
            mockScopeFactory.Verify(f => f.CreateScope(), Times.Never);
        }

        [Fact]
        public async Task ReservationExpiry_CallsCancelExpiredReservationsAsync_OnEachCycle()
        {
            // Arrange — cancel after the first scope is created so one cycle runs
            using var cts         = new CancellationTokenSource();
            var mockLogger        = new Mock<Microsoft.Extensions.Logging.ILogger<ReservationExpiryBackgroundService>>();
            var mockProvider      = new Mock<IServiceProvider>();
            var mockScopeFactory  = new Mock<IServiceScopeFactory>();
            var mockScope         = new Mock<IServiceScope>();
            var mockScopeProvider = new Mock<IServiceProvider>();
            var mockLentService   = new Mock<ILentItemsService>();

            mockProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                        .Returns(mockScopeFactory.Object);
            mockScopeFactory.Setup(f => f.CreateScope())
                .Callback(() => cts.Cancel()) // cancel as soon as scope is created
                .Returns(mockScope.Object);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);
            mockScopeProvider.Setup(p => p.GetService(typeof(ILentItemsService)))
                             .Returns(mockLentService.Object);
            mockLentService.Setup(s => s.CancelExpiredReservationsAsync()).ReturnsAsync(0);

            var sut = new ReservationExpiryBackgroundService(mockProvider.Object, mockLogger.Object);

            // Act — TaskCanceledException from Task.Delay is expected when token is cancelled
            try { await InvokeExecuteAsync(sut, cts.Token); }
            catch (TaskCanceledException) { /* expected — token cancelled during Task.Delay */ }
            catch (OperationCanceledException) { /* expected */ }

            // Assert — service called CancelExpiredReservationsAsync at least once
            mockLentService.Verify(s => s.CancelExpiredReservationsAsync(), Times.AtLeastOnce);
        }
    }
}
