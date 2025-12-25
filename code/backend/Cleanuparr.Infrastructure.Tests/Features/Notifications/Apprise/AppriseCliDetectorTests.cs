using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Apprise;

public class AppriseCliDetectorTests
{
    private readonly AppriseCliDetector _detector;

    public AppriseCliDetectorTests()
    {
        _detector = new AppriseCliDetector(Substitute.For<ILogger<AppriseCliDetector>>());
    }

    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act
        var detector = new AppriseCliDetector(Substitute.For<ILogger<AppriseCliDetector>>());

        // Assert
        Assert.NotNull(detector);
    }

    [Fact]
    public async Task GetAppriseVersionAsync_DoesNotThrow()
    {
        // Act & Assert - should handle missing CLI gracefully without throwing
        var exception = await Record.ExceptionAsync(() => _detector.GetAppriseVersionAsync());
        Assert.Null(exception);
    }
}
