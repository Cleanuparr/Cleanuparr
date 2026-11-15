using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace QBittorrent.Client.Extensions
{
    internal static class MultipartFormDataContentExtensions
    {
        internal static MultipartFormDataContent AddFile(this MultipartFormDataContent @this, 
            string name, 
            string path, 
            string contentType = "application/octet-stream")
        {
            var stream = File.OpenRead(path);
            var fileName = Path.GetFileName(path);
            var content = new StreamContent(stream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(contentType) }
            };
            @this.Add(content, name, fileName);

            return @this;
        }

        internal static MultipartFormDataContent AddNonEmptyString(this MultipartFormDataContent @this, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                @this.AddValue(name, value);
            }
            return @this;
        }

        internal static MultipartFormDataContent AddValue(this MultipartFormDataContent @this, string name, string value)
        {
            var content = new StringContent(value);
            @this.Add(content, name);
            return @this;
        }

        internal static MultipartFormDataContent AddValue(this MultipartFormDataContent @this, string name, bool value)
        {
            return @this.AddValue(name, value.ToString().ToLowerInvariant());
        }

        internal static MultipartFormDataContent AddValue<T>(this MultipartFormDataContent @this, string name, T value)
        {
            return @this.AddValue(name, value.ToString());
        }

        internal static MultipartFormDataContent AddNotNullValue(this MultipartFormDataContent @this, string name, bool? value)
        {
            if (value != null)
            {
                @this.AddValue(name, value.Value.ToString().ToLowerInvariant());
            }
            return @this;
        }

        internal static MultipartFormDataContent AddNotNullValue<T>(this MultipartFormDataContent @this, string name, T value) where T : class
        {
            if (value != null)
            {
                @this.AddValue(name, value.ToString());
            }
            return @this;
        }

        internal static MultipartFormDataContent AddNotNullValue<T>(this MultipartFormDataContent @this, string name, T? value) where T : struct
        {
            if (value != null)
            {
                @this.AddValue(name, value.Value.ToString());
            }
            return @this;
        }
    }
}
