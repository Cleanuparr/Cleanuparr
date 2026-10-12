using System.Net;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Configuration;

/// <summary>
/// Resolves the HTTP auth credential for a download client's handler.
/// </summary>
public static class DownloadClientConfigExtensions
{
    /// <summary>
    /// The credential the HTTP handler sends, or null.
    /// rTorrent, Transmission and uTorrent build their own Basic header, so Basic mode returns null for them.
    /// </summary>
    public static NetworkCredential? GetHandlerHttpAuthCredential(this DownloadClientConfig config)
    {
        bool usesDedicatedFields = config.TypeName.UsesDedicatedHttpAuthFields();

        if (config.HttpAuthType == HttpAuthType.Basic && !usesDedicatedFields)
        {
            return null;
        }

        string? username = usesDedicatedFields ? config.HttpAuthUsername : config.Username;
        string? password = usesDedicatedFields ? config.HttpAuthPassword : config.Password;

        return string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password)
            ? null
            : new NetworkCredential(username, password);
    }
}
