using AutoMapper;
using BackendTechnicalAssetsManagement.src.Classes;
using BackendTechnicalAssetsManagement.src.DTOs.User;
using BackendTechnicalAssetsManagement.src.Exceptions;
using BackendTechnicalAssetsManagement.src.IRepository;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Models.DTOs.Users;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using static BackendTechnicalAssetsManagement.src.Classes.Enums;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 1 — Authentication and Identity
    /// Covers: Register, Login, LoginMobile, ChangePassword, RefreshToken
    /// All dependencies are mocked — zero DB latency.
    /// </summary>
    public class AuthServiceTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IPasswordHashingService> _mockPasswordHashing;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<IUserValidationService> _mockUserValidation;
        private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepo;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IDevelopmentLoggerService> _mockDevLogger;

        private readonly AuthService _sut;

        public AuthServiceTests()
        {
            _mockConfig = new Mock<IConfiguration>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockMapper = new Mock<IMapper>();
            _mockPasswordHashing = new Mock<IPasswordHashingService>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockUserValidation = new Mock<IUserValidationService>();
            _mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockDevLogger = new Mock<IDevelopmentLoggerService>();

            // Non-development so dev-only logging branches are skipped
            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");

            // JWT key must be ≥ 64 chars for HMAC-SHA512
            var jwtSection = new Mock<IConfigurationSection>();
            jwtSection.Setup(s => s.Value)
                      .Returns("super-secret-test-key-that-is-definitely-long-enough-for-hmac-sha512-algorithm!!");
            _mockConfig.Setup(c => c.GetSection("AppSettings:Token")).Returns(jwtSection.Object);

            // AppDbContext passed as null — AuthService never touches it directly;
            // all DB work goes through the injected repositories.
            _sut = new AuthService(
                null!,
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
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static RegisterUserDto ValidRegisterDto(UserRole role = UserRole.Student) => new()
        {
            Username   = "jdelacruz",
            FirstName  = "Juan",
            LastName   = "Dela Cruz",
            Email      = "juan@example.com",
            PhoneNumber = "09123456789",
            Role       = role,
            Password   = "Password1!",
            ConfirmPassword = "Password1!"
        };

        private static User MakeUser(UserRole role, Guid? id = null) => new()
        {
            Id           = id ?? Guid.NewGuid(),
            Username     = "jdelacruz",
            UserRole     = role,
            PasswordHash = "hashed_password",
            Email        = "juan@example.com"
        };

        private (Mock<HttpContext> ctx, Mock<IResponseCookies> cookies) MockHttpContextWithResponse()
        {
            var cookies = new Mock<IResponseCookies>();
            var response = new Mock<HttpResponse>();
            response.Setup(r => r.Cookies).Returns(cookies.Object);
            var ctx = new Mock<HttpContext>();
            ctx.Setup(c => c.Response).Returns(response.Object);
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(ctx.Object);
            return (ctx, cookies);
        }

        private Mock<HttpContext> MockHttpContextWithClaims(Guid userId, string role)
        {
            var claims = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
                    new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.Role, role)
                }, "TestAuth"));

            var cookies = new Mock<IResponseCookies>();
            var response = new Mock<HttpResponse>();
            response.Setup(r => r.Cookies).Returns(cookies.Object);

            var ctx = new Mock<HttpContext>();
            ctx.Setup(c => c.User).Returns(claims);
            ctx.Setup(c => c.Response).Returns(response.Object);
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(ctx.Object);
            return ctx;
        }

        // ══════════════════════════════════════════════════════════════════════
        // REGISTER
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Register_Fails_When_UserRoleHierarchyIsViolated()
        {
            // Staff trying to create an Admin — must be rejected
            var creatorId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(creatorId))
                         .ReturnsAsync(MakeUser(UserRole.Staff, creatorId));

            Func<Task> act = () => _sut.Register(ValidRegisterDto(UserRole.Admin), creatorId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*Staff*cannot create*Admin*");
        }

        [Fact]
        public async Task Register_Fails_When_PhoneNumberDoesNotStartWith09()
        {
            var creatorId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(creatorId))
                         .ReturnsAsync(MakeUser(UserRole.Admin, creatorId));
            _mockUserValidation.Setup(v => v.ValidateUniqueUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Mapper writes an invalid phone onto the new user object
            _mockMapper.Setup(m => m.Map(It.IsAny<RegisterUserDto>(), It.IsAny<User>()))
                       .Callback<object, object>((_, dest) =>
                       {
                           if (dest is User u) u.PhoneNumber = "08000000000";
                       });

            var dto = ValidRegisterDto();
            dto.PhoneNumber = "08000000000";

            Func<Task> act = () => _sut.Register(dto, creatorId);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*must start with 09*");
        }

        [Fact]
        public async Task Register_Fails_When_PasswordDoesNotMeetComplexityRules()
        {
            var creatorId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(creatorId))
                         .ReturnsAsync(MakeUser(UserRole.Admin, creatorId));
            _mockUserValidation.Setup(v => v.ValidateUniqueUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockMapper.Setup(m => m.Map(It.IsAny<RegisterUserDto>(), It.IsAny<User>()))
                       .Callback<object, object>((_, dest) =>
                       {
                           if (dest is User u) u.PhoneNumber = "09123456789";
                       });

            var dto = ValidRegisterDto();
            dto.Password = "weakpass"; // no uppercase / digit / special char

            Func<Task> act = () => _sut.Register(dto, creatorId);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Password must be at least 8 characters*");
        }

        [Fact]
        public async Task Register_Fails_When_PhoneNumberIsAlreadyUsed()
        {
            var creatorId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(creatorId))
                         .ReturnsAsync(MakeUser(UserRole.Admin, creatorId));
            _mockUserValidation.Setup(v => v.ValidateUniqueUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockMapper.Setup(m => m.Map(It.IsAny<RegisterUserDto>(), It.IsAny<User>()))
                       .Callback<object, object>((_, dest) =>
                       {
                           if (dest is User u) u.PhoneNumber = "09123456789";
                       });

            // Phone already belongs to another user
            _mockUserRepo.Setup(r => r.GetByPhoneNumberAsync("09123456789"))
                         .ReturnsAsync(MakeUser(UserRole.Student));

            Func<Task> act = () => _sut.Register(ValidRegisterDto(), creatorId);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Phone number is already used*");
        }

        [Theory]
        [InlineData(UserRole.Student)]
        [InlineData(UserRole.Teacher)]
        [InlineData(UserRole.Staff)]
        public async Task Register_Succeeds_And_InstantiatesCorrectDerivedClass(UserRole role)
        {
            var creatorId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(creatorId))
                         .ReturnsAsync(MakeUser(UserRole.Admin, creatorId));
            _mockUserValidation.Setup(v => v.ValidateUniqueUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockUserRepo.Setup(r => r.GetByPhoneNumberAsync(It.IsAny<string>()))
                         .ReturnsAsync((User?)null);

            User? captured = null;
            _mockMapper.Setup(m => m.Map(It.IsAny<RegisterUserDto>(), It.IsAny<User>()))
                       .Callback<object, object>((_, dest) =>
                       {
                           captured = dest as User;
                           if (captured != null) captured.PhoneNumber = "09123456789";
                       });

            _mockPasswordHashing.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");
            _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(new User());
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _mockMapper.Setup(m => m.Map<UserDto>(It.IsAny<User>())).Returns(new UserDto());

            await _sut.Register(ValidRegisterDto(role), creatorId);

            captured.Should().NotBeNull();
            switch (role)
            {
                case UserRole.Student: captured.Should().BeOfType<Student>(); break;
                case UserRole.Teacher: captured.Should().BeOfType<Teacher>(); break;
                case UserRole.Staff:   captured.Should().BeOfType<Staff>();   break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // LOGIN
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Login_ThrowsInvalidCredentials_WhenUserNotFound()
        {
            _mockUserRepo.Setup(r => r.GetByIdentifierAsync(It.IsAny<string>()))
                         .ReturnsAsync((User?)null);

            Func<Task> act = () => _sut.Login(
                new LoginUserDto { Identifier = "nobody", Password = "Password1!" });

            await act.Should().ThrowAsync<InvalidCredentialsException>()
                .WithMessage("*Invalid username or password*");
        }

        [Fact]
        public async Task Login_ThrowsInvalidCredentials_WhenPasswordHashFails()
        {
            _mockUserRepo.Setup(r => r.GetByIdentifierAsync(It.IsAny<string>()))
                         .ReturnsAsync(MakeUser(UserRole.Student));
            _mockPasswordHashing.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
                                 .Returns(false);

            Func<Task> act = () => _sut.Login(
                new LoginUserDto { Identifier = "jdelacruz", Password = "WrongPass1!" });

            await act.Should().ThrowAsync<InvalidCredentialsException>()
                .WithMessage("*Invalid username or password*");
        }

        [Fact]
        public async Task Login_Succeeds_RevokesOldTokens_ReturnsNewTokens()
        {
            var user = MakeUser(UserRole.Student);
            _mockUserRepo.Setup(r => r.GetByIdentifierAsync(It.IsAny<string>())).ReturnsAsync(user);
            _mockPasswordHashing.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
                                 .Returns(true);
            _mockRefreshTokenRepo.Setup(r => r.RevokeAllForUserAsync(user.Id)).Returns(Task.CompletedTask);
            _mockRefreshTokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
            _mockRefreshTokenRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            MockHttpContextWithResponse();
            _mockMapper.Setup(m => m.Map<UserDto>(user))
                       .Returns(new UserDto { Id = user.Id, Username = user.Username });

            var result = await _sut.Login(
                new LoginUserDto { Identifier = "jdelacruz", Password = "Password1!" });

            _mockRefreshTokenRepo.Verify(r => r.RevokeAllForUserAsync(user.Id), Times.Once);
            _mockRefreshTokenRepo.Verify(r => r.AddAsync(It.IsAny<RefreshToken>()), Times.Once);
            result.Id.Should().Be(user.Id);
        }

        [Fact]
        public async Task LoginMobile_Succeeds_ReturnsTokensInDTOWithoutCookies()
        {
            var user = MakeUser(UserRole.Student);
            _mockUserRepo.Setup(r => r.GetByIdentifierAsync(It.IsAny<string>())).ReturnsAsync(user);
            _mockPasswordHashing.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
                                 .Returns(true);
            _mockRefreshTokenRepo.Setup(r => r.RevokeAllForUserAsync(user.Id)).Returns(Task.CompletedTask);
            _mockRefreshTokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
            _mockRefreshTokenRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _mockMapper.Setup(m => m.Map<UserDto>(user))
                       .Returns(new UserDto { Id = user.Id, Username = user.Username });

            var result = await _sut.LoginMobile(
                new LoginUserDto { Identifier = "jdelacruz", Password = "Password1!" });

            result.User.Should().NotBeNull();
            result.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            // No HttpContext interaction — cookies must NOT be set for mobile
            _mockHttpContextAccessor.Verify(a => a.HttpContext, Times.Never);
        }

        // ══════════════════════════════════════════════════════════════════════
        // CHANGE PASSWORD
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ChangePassword_ThrowsUnauthorized_WhenAdminChangesSuperAdmin()
        {
            var adminId      = Guid.NewGuid();
            var superAdminId = Guid.NewGuid();

            MockHttpContextWithClaims(adminId, "Admin");

            _mockUserRepo.Setup(r => r.GetByIdAsync(superAdminId))
                         .ReturnsAsync(MakeUser(UserRole.SuperAdmin, superAdminId));

            Func<Task> act = () => _sut.ChangePassword(superAdminId,
                new ChangePasswordDto { NewPassword = "NewPass1!", ConfirmPassword = "NewPass1!" });

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*SuperAdmin*password*");
        }

        [Fact]
        public async Task ChangePassword_Succeeds_HashesNewPassword_And_RevokesAllTokens()
        {
            var userId = Guid.NewGuid();
            var user   = MakeUser(UserRole.Student, userId);

            MockHttpContextWithClaims(userId, "Student");

            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockPasswordHashing.Setup(p => p.HashPassword("NewPass1!")).Returns("new_hash");
            _mockUserRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);
            _mockRefreshTokenRepo.Setup(r => r.RevokeAllForUserAsync(userId)).Returns(Task.CompletedTask);
            _mockUserRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            await _sut.ChangePassword(userId,
                new ChangePasswordDto { NewPassword = "NewPass1!", ConfirmPassword = "NewPass1!" });

            user.PasswordHash.Should().Be("new_hash");
            _mockRefreshTokenRepo.Verify(r => r.RevokeAllForUserAsync(userId), Times.Once);
            _mockUserRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // REFRESH TOKEN
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task RefreshToken_ThrowsException_IfCookieIsMissing()
        {
            var reqCookies = new Mock<IRequestCookieCollection>();
            reqCookies.Setup(c => c["4CLC-Auth-SRT"]).Returns((string?)null);
            var request = new Mock<HttpRequest>();
            request.Setup(r => r.Cookies).Returns(reqCookies.Object);
            var ctx = new Mock<HttpContext>();
            ctx.Setup(c => c.Request).Returns(request.Object);
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(ctx.Object);

            Func<Task> act = () => _sut.RefreshToken();

            await act.Should().ThrowAsync<RefreshTokenException>()
                .WithMessage("*cookie is missing*");
        }

        [Fact]
        public async Task RefreshToken_DetectsReplayAttack_IfTokenIsAlreadyRevoked()
        {
            const string token = "revoked-token-string";

            var reqCookies = new Mock<IRequestCookieCollection>();
            reqCookies.Setup(c => c["4CLC-Auth-SRT"]).Returns(token);
            var request = new Mock<HttpRequest>();
            request.Setup(r => r.Cookies).Returns(reqCookies.Object);

            var resCookies = new Mock<IResponseCookies>();
            var response   = new Mock<HttpResponse>();
            response.Setup(r => r.Cookies).Returns(resCookies.Object);

            var ctx = new Mock<HttpContext>();
            ctx.Setup(c => c.Request).Returns(request.Object);
            ctx.Setup(c => c.Response).Returns(response.Object);
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(ctx.Object);

            _mockRefreshTokenRepo.Setup(r => r.GetByTokenAsync(token))
                .ReturnsAsync(new RefreshToken
                {
                    Token      = token,
                    IsRevoked  = true,
                    ExpiresAt  = DateTime.UtcNow.AddDays(7),
                    UserId     = Guid.NewGuid()
                });

            Func<Task> act = () => _sut.RefreshToken();

            await act.Should().ThrowAsync<RefreshTokenException>()
                .WithMessage("*revoked*");
        }
    }
}
