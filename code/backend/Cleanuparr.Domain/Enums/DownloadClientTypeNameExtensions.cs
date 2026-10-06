namespace Cleanuparr.Domain.Enums;

public static class DownloadClientTypeNameExtensions
{
    private static readonly Dictionary<DownloadClientTypeName, DownloadClientAuthField[]> AuthFields = new()
    {
        [DownloadClientTypeName.qBittorrent] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.Deluge] = [DownloadClientAuthField.Password],
        [DownloadClientTypeName.Transmission] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.uTorrent] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.rTorrent] = [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
        [DownloadClientTypeName.Sabnzbd] = [DownloadClientAuthField.ApiKey],
    };

    public static bool SupportsAuthField(this DownloadClientTypeName typeName, DownloadClientAuthField field) =>
        AuthFields.TryGetValue(typeName, out var fields) && fields.Contains(field);

    /// <summary>
    /// True when the field is the client's only credential, so leaving it blank means it cannot authenticate at all.
    /// </summary>
    public static bool RequiresAuthField(this DownloadClientTypeName typeName, DownloadClientAuthField field) =>
        AuthFields.TryGetValue(typeName, out var fields) && fields is [var only] && only == field;
}
