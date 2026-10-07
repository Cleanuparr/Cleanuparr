using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Base contract every download client implements, regardless of protocol or capabilities.
/// Protocol-specific behaviour lives behind the <c>I*Capable</c> interfaces.
/// </summary>
public interface IDownloadService : IDisposable
{
    DownloadClientConfig ClientConfig { get; }

    Task LoginAsync();

    /// <summary>
    /// Performs a health check on the download client
    /// </summary>
    /// <returns>The health check result</returns>
    Task<HealthCheckResult> HealthCheckAsync();

    /// <summary>
    /// Deletes a download item.
    /// </summary>
    /// <param name="item">The download item.</param>
    /// <param name="deleteSourceFiles">Whether to delete the source files along with the download. Defaults to true.</param>
    Task DeleteDownload(IDownloadItem item, bool deleteSourceFiles);
}
