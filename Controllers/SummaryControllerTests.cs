using BackendTechnicalAssetsManagement.src.Controllers;
using BackendTechnicalAssetsManagement.src.DTOs.Statistics;
using BackendTechnicalAssetsManagement.src.IService;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BackendTechincalAssetsManagementTest.Controllers
{
    /// <summary>
    /// Part 11f — SummaryController
    /// Max per-test: 200 ms | ISummaryService fully mocked.
    /// Tests verify HTTP status codes and ApiResponse shape.
    /// </summary>
    public class SummaryControllerTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────
        private readonly Mock<ISummaryService> _mockSummaryService;
        private readonly SummaryController     _sut;

        public SummaryControllerTests()
        {
            _mockSummaryService = new Mock<ISummaryService>();
            _sut                = new SummaryController(_mockSummaryService.Object);
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetOverallSummary_Returns_Ok_WithSummaryDto()
        {
            // Arrange
            var summary = new SummaryDto
            {
                TotalItems       = 50,
                TotalLentItems   = 20,
                TotalActiveUsers = 30
            };
            _mockSummaryService.Setup(s => s.GetOverallSummaryAsync()).ReturnsAsync(summary);

            // Act
            var result = await _sut.GetOverallSummary();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<SummaryDto>;
            body!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task GetOverallSummary_WrapsData_InApiResponse()
        {
            // Arrange
            var summary = new SummaryDto { TotalItems = 10 };
            _mockSummaryService.Setup(s => s.GetOverallSummaryAsync()).ReturnsAsync(summary);

            // Act
            var result = await _sut.GetOverallSummary();

            // Assert
            var body = ((OkObjectResult)result.Result!).Value as ApiResponse<SummaryDto>;
            body!.Data.Should().NotBeNull();
            body.Message.Should().Contain("summary");
        }
    }
}
