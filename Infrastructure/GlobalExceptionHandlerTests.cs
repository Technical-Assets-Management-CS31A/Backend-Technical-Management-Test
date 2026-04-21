using BackendTechnicalAssetsManagement.src.Exceptions;
using BackendTechnicalAssetsManagement.src.Middleware;
using BackendTechnicalAssetsManagement.src.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BackendTechincalAssetsManagementTest.Infrastructure
{
    /// <summary>
    /// Part 12a — GlobalExceptionHandler middleware
    /// Max per-test: 200 ms | Tests invoke the middleware directly with a throwing RequestDelegate.
    /// Reads the response body and deserializes ApiResponse to assert status code and Success = false.
    /// </summary>
    public class GlobalExceptionHandlerTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static GlobalExceptionHandler MakeSut() =>
            new(
                _ => Task.CompletedTask,                          // _next placeholder (overridden per test)
                new Mock<ILogger<GlobalExceptionHandler>>().Object);

        /// <summary>
        /// Invokes the middleware with a custom next delegate that throws the given exception.
        /// Returns the HTTP status code and the deserialized ApiResponse body.
        /// </summary>
        private static async Task<(int StatusCode, ApiResponse<object>? Body)> InvokeWithException(
            Exception exception)
        {
            var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
            RequestDelegate throwingNext = _ => throw exception;

            var handler = new GlobalExceptionHandler(throwingNext, mockLogger.Object);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await handler.InvokeAsync(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var body = JsonSerializer.Deserialize<ApiResponse<object>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return (context.Response.StatusCode, body);
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task InvokeAsync_Returns_403_For_UnauthorizedAccessException()
        {
            // Arrange + Act
            var (statusCode, body) = await InvokeWithException(
                new UnauthorizedAccessException("Forbidden"));

            // Assert
            statusCode.Should().Be(403);
            body!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task InvokeAsync_Returns_404_For_KeyNotFoundException()
        {
            // Arrange + Act
            var (statusCode, body) = await InvokeWithException(
                new KeyNotFoundException("Not found"));

            // Assert
            statusCode.Should().Be(404);
            body!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task InvokeAsync_Returns_400_For_ArgumentException()
        {
            // Arrange + Act
            var (statusCode, body) = await InvokeWithException(
                new ArgumentException("Bad input"));

            // Assert
            statusCode.Should().Be(400);
            body!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task InvokeAsync_Returns_400_For_InvalidOperationException()
        {
            // Arrange + Act
            var (statusCode, body) = await InvokeWithException(
                new InvalidOperationException("Invalid state"));

            // Assert
            statusCode.Should().Be(400);
            body!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task InvokeAsync_Returns_401_For_InvalidCredentialsException()
        {
            // Arrange + Act
            var (statusCode, body) = await InvokeWithException(
                new InvalidCredentialsException("Bad creds"));

            // Assert
            statusCode.Should().Be(401);
            body!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task InvokeAsync_Returns_500_For_UnhandledException()
        {
            // Arrange + Act
            var (statusCode, body) = await InvokeWithException(
                new Exception("Unexpected"));

            // Assert
            statusCode.Should().Be(500);
            body!.Success.Should().BeFalse();
        }
    }
}
