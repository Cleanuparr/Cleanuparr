namespace Cleanuparr.Persistence.Startup;

/// <summary>
/// Parses and strips URL userinfo (<c>user:pass@host</c>).
/// </summary>
public static class UserInfoMigration
{
    /// <summary>
    /// Splits raw userinfo on the first colon and percent-decodes both sides.
    /// No colon means no password.
    /// An empty username becomes null.
    /// </summary>
    public static UserInfoCredential ParseCredential(string userInfo)
    {
        int separatorIndex = userInfo.IndexOf(':');

        string usernamePart = separatorIndex < 0 ? userInfo : userInfo[..separatorIndex];
        string? passwordPart = separatorIndex < 0 ? null : userInfo[(separatorIndex + 1)..];

        string? username = usernamePart.Length == 0 ? null : Uri.UnescapeDataString(usernamePart);
        string? password = passwordPart is null ? null : Uri.UnescapeDataString(passwordPart);

        return new UserInfoCredential(username, password);
    }

    /// <summary>
    /// Removes the userinfo and its <c>@</c>, leaving the rest of the string as is.
    /// <see cref="UriBuilder"/> would normalize the scheme and add a default port and trailing slash.
    /// </summary>
    public static Uri StripUserInfo(Uri host)
    {
        string original = host.OriginalString;
        int authorityStart = original.IndexOf("://", StringComparison.Ordinal) + 3;

        int authorityEnd = authorityStart;
        while (authorityEnd < original.Length
            && original[authorityEnd] != '/'
            && original[authorityEnd] != '?'
            && original[authorityEnd] != '#')
        {
            authorityEnd++;
        }

        int atIndex = original.IndexOf('@', authorityStart, authorityEnd - authorityStart);
        if (atIndex < 0)
        {
            return host;
        }

        string rebuilt = original.Remove(authorityStart, atIndex - authorityStart + 1);
        return new Uri(rebuilt, UriKind.RelativeOrAbsolute);
    }
}
