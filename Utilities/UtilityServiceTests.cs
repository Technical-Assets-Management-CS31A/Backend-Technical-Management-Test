using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;

namespace BackendTechincalAssetsManagementTest.Utilities
{
    /// <summary>
    /// Part 10 — Utility & Infrastructure Services
    /// Covers: PasswordHashingService (pure logic, no mocks needed)
    /// </summary>
    public class PasswordHashingServiceTests
    {
        private readonly PasswordHashingService _sut = new();

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
}
