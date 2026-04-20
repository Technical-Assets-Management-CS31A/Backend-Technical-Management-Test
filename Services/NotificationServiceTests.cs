using BackendTechnicalAssetsManagement.src.Hubs;
using BackendTechnicalAssetsManagement.src.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackendTechincalAssetsManagementTest.Services
{
    /// <summary>
    /// Part 8 — Real-Time Notifications (NotificationService)
    /// Covers: SendNewPendingRequest, SendApproval, SendStatusChange, SendBroadcast
    /// Uses SignalR mock pattern — verifies SendAsync invocations.
    /// </summary>
    public class NotificationServiceTests
    {
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients>                  _mockClients;
        private readonly Mock<IClientProxy>                 _mockGroupProxy;
        private readonly Mock<IClientProxy>                 _mockAllProxy;
        private readonly Mock<ILogger<NotificationService>> _mockLogger;

        private readonly NotificationService _sut;

        public NotificationServiceTests()
        {
            _mockGroupProxy = new Mock<IClientProxy>();
            _mockAllProxy   = new Mock<IClientProxy>();
            _mockClients    = new Mock<IHubClients>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockLogger     = new Mock<ILogger<NotificationService>>();

            // Group("admin_staff") and Group("user_xxx") both return the group proxy
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockGroupProxy.Object);
            _mockClients.Setup(c => c.All).Returns(_mockAllProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);

            _sut = new NotificationService(_mockHubContext.Object, _mockLogger.Object);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SEND NEW PENDING REQUEST
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SendNewPendingRequest_Invokes_SendAsync_On_AdminStaff_Group()
        {
            await _sut.SendNewPendingRequestNotificationAsync(
                Guid.NewGuid(), "Projector", "Juan Dela Cruz", null);

            _mockClients.Verify(c => c.Group("admin_staff"), Times.Once);
            _mockGroupProxy.Verify(
                p => p.SendCoreAsync(
                    "ReceiveNewPendingRequest",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendNewPendingRequest_DoesNotThrow_When_HubFails()
        {
            _mockGroupProxy
                .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("SignalR unavailable"));

            // Service swallows exceptions internally — should not propagate
            Func<Task> act = () => _sut.SendNewPendingRequestNotificationAsync(
                Guid.NewGuid(), "Projector", "Juan", null);

            await act.Should().NotThrowAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // SEND APPROVAL NOTIFICATION
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SendApproval_Sends_To_User_Group_When_UserId_Provided()
        {
            var userId = Guid.NewGuid();

            await _sut.SendApprovalNotificationAsync(
                Guid.NewGuid(), userId, "Projector", "Juan");

            _mockClients.Verify(c => c.Group($"user_{userId}"), Times.Once);
            _mockGroupProxy.Verify(
                p => p.SendCoreAsync(
                    "ReceiveApprovalNotification",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeast(1));
        }

        [Fact]
        public async Task SendApproval_Always_Notifies_AdminStaff_Group()
        {
            await _sut.SendApprovalNotificationAsync(
                Guid.NewGuid(), null, "Projector", "Juan");

            _mockClients.Verify(c => c.Group("admin_staff"), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SEND STATUS CHANGE NOTIFICATION
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SendStatusChange_Sends_To_User_Group_When_UserId_Provided()
        {
            var userId = Guid.NewGuid();

            await _sut.SendStatusChangeNotificationAsync(
                Guid.NewGuid(), userId, "Projector", "Pending", "Approved");

            _mockClients.Verify(c => c.Group($"user_{userId}"), Times.Once);
            _mockGroupProxy.Verify(
                p => p.SendCoreAsync(
                    "ReceiveStatusChangeNotification",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeast(1));
        }

        [Fact]
        public async Task SendStatusChange_Always_Notifies_AdminStaff_Group()
        {
            await _sut.SendStatusChangeNotificationAsync(
                Guid.NewGuid(), null, "Projector", "Borrowed", "Returned");

            _mockClients.Verify(c => c.Group("admin_staff"), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SEND BROADCAST NOTIFICATION
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SendBroadcast_Invokes_SendAsync_On_All_Clients()
        {
            await _sut.SendBroadcastNotificationAsync("System maintenance in 5 minutes");

            _mockAllProxy.Verify(
                p => p.SendCoreAsync(
                    "ReceiveBroadcastNotification",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendBroadcast_DoesNotThrow_When_HubFails()
        {
            _mockAllProxy
                .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Hub down"));

            Func<Task> act = () => _sut.SendBroadcastNotificationAsync("test");

            await act.Should().NotThrowAsync();
        }
    }
}
