using System;
using System.Linq;

namespace QBittorrent.Client.Extensions
{
    internal static class UriExtensions
    {
        internal static Uri EnsureTrailingSlash(this Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

#if NET6_0_OR_GREATER
            if (uri.AbsolutePath.EndsWith('/'))
                return uri;
#else
            if (uri.AbsolutePath.EndsWith("/"))
                return uri;
#endif

            var builder = new UriBuilder(uri);
            builder.Path += "/";
            return builder.Uri;
        }

        internal static Uri WithQueryParameters(this Uri uri, params (string key, string value)[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return uri;

            var builder = new UriBuilder(uri)
            {
                Query = string.Join("&", parameters
                    .Where(t => t.value != null)
                    .Select(t => $"{Uri.EscapeDataString(t.key)}={Uri.EscapeDataString(t.value)}"))
            };
            return builder.Uri;
        }
    }
}