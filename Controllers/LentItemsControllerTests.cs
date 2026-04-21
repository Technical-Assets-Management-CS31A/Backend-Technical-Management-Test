using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs;
using BackendTechnicalAssetsManagement.src.DTOs.LentItems;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11d — LentItemsController
    /// Max per-test: 200 ms | ILentItemsService fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class LentItemsControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<ILentItemsService> _mockService;
        private readonly LentItemsController     _sut;

        public LentItemsControllerTests()
        {
            _mockService = new Mock<ILentItemsService>();
            _sut         = new LentItemsController(_mockService.Object);
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ClaimsPrincipal MakeUser(string role, Guid? id = null) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, (id ?? Guid.NewGuid()).ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "Test"));

        private void SetUser(ClaimsPrincipal user)
        {
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        private static LentItemsDto MakeLentItemsDto(Guid? id = null) => new()
        {
            Id             = id ?? Guid.NewGuid(),
            BorrowerRole   = "Student",
            BorrowerFullName = "Juan Dela Cruz",
            Status         = "Borrowed"
        };

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task Borrow_Returns_201_OnSuccess()
        {
            // Arrange
            var dto     = new CreateBorrowDto();
            var created = MakeLentItemsDto();
            SetUser(MakeUser("Staff"));
            _mockService.Setup(s => s.AddBorrowAsync(dto)).ReturnsAsync(created);

            // Act
            var result = await _sut.Borrow(dto);

            // Assert
            result.Result.Should().BeOfType<CreatedAtActionResult>();
            var createdResult = (CreatedAtActionResult)result.Result!;
            createdResult.StatusCode.Should().Be(201);
        }

        [Fact]
        public async Task Reserve_Returns_201_OnSuccess()
        {
            // Arrange
            var dto     = new CreateReservationDto();
            var created = MakeLentItemsDto();
            SetUser(MakeUser("Student"));
            _mockService.Setup(s => s.AddReservationAsync(dto)).ReturnsAsync(created);

            // Act
            var result = await _sut.Reserve(dto);

            // Assert
            result.Result.Should().BeOfType<CreatedAtActionResult>();
            ((CreatedAtActionResult)result.Result!).StatusCode.Should().Be(201);
        }

        [Fact]
        public async Task AddForGuest_Returns_Unauthorized_WhenClaimMissing()
        {
            // Arrange — no NameIdentifier claim
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Role, "Staff")
                    }, "Test"))
                }
            };

            // Act
            var result = await _sut.AddForGuest(new CreateLentItemsForGuestDto());

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task GetById_Returns_NotFound_WhenNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            SetUser(MakeUser("Staff"));
            _mockService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((LentItemsDto?)null);

            // Act
            var result = await _sut.GetById(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Update_Returns_NotFound_WhenServiceReturnsFalse()
        {
            // Arrange
            var id = Guid.NewGuid();
            SetUser(MakeUser("Staff"));
            _mockService.Setup(s => s.UpdateAsync(id, It.IsAny<UpdateLentItemDto>())).ReturnsAsync(false);

            // Act
            var result = await _sut.Update(id, new UpdateLentItemDto());

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task ArchiveLentItems_Returns_NotFound_WhenNotFound()
        {
            // Arrange
            var id = Guid.NewGuid();
            SetUser(MakeUser("Staff"));
            _mockService.Setup(s => s.ArchiveLentItems(id)).ReturnsAsync((false, "not found"));

            // Act
            var result = await _sut.ArchiveLentItems(id);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetByDateTime_Returns_BadRequest_WhenDateInvalid()
        {
            // Arrange
            SetUser(MakeUser("Staff"));

            // Act
            var result = await _sut.GetByDateTime("not-a-date");

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}
