namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public interface IAppriseCliDetector
{
    Task<string?> GetAppriseVersionAsync();
}
