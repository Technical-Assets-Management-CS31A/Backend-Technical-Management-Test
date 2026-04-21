using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Moq;

namespace BackendTechincalAssetsManagementTest.Infrastructure
{
    /// <summary>
    /// Part 12c — UserValidationService
    /// Max per-test: 200 ms | IUserRepository fully mocked.
    /// Tests verify that duplicate username, email, and phone number throw the correct exceptions.
    /// </summary>
    public class UserValidationServiceTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IUserRepository>  _mockUserRepo;
        private readonly UserValidationService  _sut;

        public UserValidationServiceTests()
        {
            _mockUserRepo = new Mock<IUserRepository>();
            _sut          = new UserValidationService(_mockUserRepo.Object);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static User MakeExistingUser() => new Student
        {
            Id        = Guid.NewGuid(),
            Username  = "existing",
            Email     = "existing@dlsu.edu.ph",
            LastName  = "Dela Cruz",
            FirstName = "Juan"
        };

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task ValidateUniqueUser_Throws_WhenUsernameAlreadyTaken()
        {
            // Arrange
            var existing = MakeExistingUser();
            _mockUserRepo.Setup(r => r.GetByUsernameAsync("taken")).ReturnsAsync(existing);
            _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserRepo.Setup(r => r.GetByPhoneNumberAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

            // Act
            Func<Task> act = () => _sut.ValidateUniqueUserAsync("taken", "new@email.com", "09123456789");

            // Assert
            await act.Should().ThrowAsync<Exception>()
                     .WithMessage("*taken*");
        }

        [Fact]
        public async Task ValidateUniqueUser_Throws_WhenEmailAlreadyExists()
        {
            // Arrange
            var existing = MakeExistingUser();
            _mockUserRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserRepo.Setup(r => r.GetByEmailAsync("dup@dlsu.edu.ph")).ReturnsAsync(existing);
            _mockUserRepo.Setup(r => r.GetByPhoneNumberAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

            // Act
            Func<Task> act = () => _sut.ValidateUniqueUserAsync("newuser", "dup@dlsu.edu.ph", "09123456789");

            // Assert
            await act.Should().ThrowAsync<Exception>()
                     .WithMessage("*already exist*");
        }

        [Fact]
        public async Task ValidateUniqueUser_Throws_WhenPhoneNumberAlreadyUsed()
        {
            // Arrange
            var existing = MakeExistingUser();
            _mockUserRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserRepo.Setup(r => r.GetByPhoneNumberAsync("09123456789")).ReturnsAsync(existing);

            // Act
            Func<Task> act = () => _sut.ValidateUniqueUserAsync("newuser", "new@email.com", "09123456789");

            // Assert
            await act.Should().ThrowAsync<Exception>()
                     .WithMessage("*Phone Number*");
        }
    }
}
