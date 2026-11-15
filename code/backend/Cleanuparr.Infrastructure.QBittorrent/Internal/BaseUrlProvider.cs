using System;
using QBittorrent.Client.Extensions;

namespace QBittorrent.Client.Internal
{
    internal abstract class BaseUrlProvider
    {
        private readonly Uri _baseUri;

        private protected BaseUrlProvider(Uri baseUri)
        {
            _baseUri = baseUri.EnsureTrailingSlash();
        }

        private protected Uri Create(string relativeUri) => new Uri(_baseUri, relativeUri);

        private protected Uri Create(string relativeUri, params (string key, string value)[] parameters)
        {
            return Create(relativeUri).WithQueryParameters(parameters);
        }
    }
}
