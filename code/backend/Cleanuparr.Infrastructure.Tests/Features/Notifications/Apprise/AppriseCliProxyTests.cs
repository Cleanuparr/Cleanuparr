using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Apprise;

public class AppriseCliProxyTests
{
    private readonly AppriseCliProxy _proxy;

    public AppriseCliProxyTests()
    {
        _proxy = new AppriseCliProxy();
    }

    private static ApprisePayload CreatePayload(string title = "Test Title", string body = "Test Body")
    {
        return new ApprisePayload
        {
            Title = title,
            Body = body,
            Type = "info"
        };
    }

    private static AppriseConfig CreateConfig(string? serviceUrls = null)
    {
        return new AppriseConfig
        {
            ServiceUrls = serviceUrls
        };
    }

    #region SendNotification Validation Tests

    [Fact]
    public async Task SendNotification_WhenServiceUrlsIsNull_ThrowsAppriseException()
    {
        // Arrange
        var config = CreateConfig(null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            _proxy.SendNotification(CreatePayload(), config));
        Assert.Contains("No service URLs configured", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenServiceUrlsIsEmpty_ThrowsAppriseException()
    {
        // Arrange
        var config = CreateConfig("");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            _proxy.SendNotification(CreatePayload(), config));
        Assert.Contains("No service URLs configured", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenServiceUrlsIsWhitespace_ThrowsAppriseException()
    {
        // Arrange
        var config = CreateConfig("   \n   \n   ");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            _proxy.SendNotification(CreatePayload(), config));
        Assert.Contains("No service URLs configured", ex.Message);
    }

    #endregion
}
