using BackendTechnicalAssetsManagement.src.Authorization;
using BackendTechnicalAssetsManagement.src.Classes;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BackendTechincalAssetsManagementTest.Infrastructure
{
    /// <summary>
    /// Part 12b — Authorization Handlers
    /// Max per-test: 200 ms | Pure logic tests — no mocks needed.
    /// Covers: SuperAdminBypassHandler, ViewProfileRequirement.ViewProfileHandler
    /// </summary>
    public class AuthorizationHandlerTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static ClaimsPrincipal MakeUser(string role, Guid? id = null) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, (id ?? Guid.NewGuid()).ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "Test"));

        private static User MakeUserEntity(Guid? id = null) => new Student
        {
            Id        = id ?? Guid.NewGuid(),
            Username  = "jdelacruz",
            Email     = "juan@dlsu.edu.ph",
            LastName  = "Dela Cruz",
            FirstName = "Juan"
        };

        // ══════════════════════════════════════════════════════════════════════
        // SuperAdminBypassHandler
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SuperAdminBypassHandler_Succeeds_AllRequirements_WhenUserIsSuperAdmin()
        {
            // Arrange
            var handler    = new SuperAdminBypassHandler();
            var superAdmin = MakeUser("SuperAdmin");

            var req1 = new ViewProfileRequirement();
            var req2 = new ViewProfileRequirement();
            var requirements = new List<IAuthorizationRequirement> { req1, req2 };

            var context = new AuthorizationHandlerContext(requirements, superAdmin, null);

            // Act
            await handler.HandleAsync(context);

            // Assert — both requirements should be succeeded
            context.HasSucceeded.Should().BeTrue();
            context.PendingRequirements.Should().BeEmpty();
        }

        [Fact]
        public async Task SuperAdminBypassHandler_DoesNotSucceed_WhenUserIsNotSuperAdmin()
        {
            // Arrange
            var handler = new SuperAdminBypassHandler();
            var admin   = MakeUser("Admin");

            var req          = new ViewProfileRequirement();
            var requirements = new List<IAuthorizationRequirement> { req };
            var context      = new AuthorizationHandlerContext(requirements, admin, null);

            // Act
            await handler.HandleAsync(context);

            // Assert — no requirements succeeded; pending requirements remain
            context.HasSucceeded.Should().BeFalse();
            context.PendingRequirements.Should().Contain(req);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ViewProfileRequirement.ViewProfileHandler
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ViewProfileHandler_Succeeds_WhenUserIsAdmin()
        {
            // Arrange
            var handler  = new ViewProfileRequirement.ViewProfileHandler();
            var admin    = MakeUser("Admin");
            var resource = MakeUserEntity();

            var req     = new ViewProfileRequirement();
            var context = new AuthorizationHandlerContext(
                new[] { req }, admin, resource);

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task ViewProfileHandler_Succeeds_WhenUserIsStaff()
        {
            // Arrange
            var handler  = new ViewProfileRequirement.ViewProfileHandler();
            var staff    = MakeUser("Staff");
            var resource = MakeUserEntity();

            var req     = new ViewProfileRequirement();
            var context = new AuthorizationHandlerContext(
                new[] { req }, staff, resource);

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task ViewProfileHandler_Succeeds_WhenUserViewsOwnProfile()
        {
            // Arrange
            var userId   = Guid.NewGuid();
            var handler  = new ViewProfileRequirement.ViewProfileHandler();
            var student  = MakeUser("Student", userId);
            var resource = MakeUserEntity(userId); // same ID as caller

            var req     = new ViewProfileRequirement();
            var context = new AuthorizationHandlerContext(
                new[] { req }, student, resource);

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task ViewProfileHandler_Fails_WhenStudentViewsOtherProfile()
        {
            // Arrange
            var callerId = Guid.NewGuid();
            var otherId  = Guid.NewGuid(); // different from callerId
            var handler  = new ViewProfileRequirement.ViewProfileHandler();
            var student  = MakeUser("Student", callerId);
            var resource = MakeUserEntity(otherId);

            var req     = new ViewProfileRequirement();
            var context = new AuthorizationHandlerContext(
                new[] { req }, student, resource);

            // Act
            await handler.HandleAsync(context);

            // Assert — requirement not succeeded
            context.HasSucceeded.Should().BeFalse();
        }
    }
}
