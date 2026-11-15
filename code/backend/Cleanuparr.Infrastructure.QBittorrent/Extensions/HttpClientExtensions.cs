using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace QBittorrent.Client.Extensions
{
    internal static class HttpClientExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<string> GetStringWithCancellationAsync(this HttpClient client, 
            Uri uri, 
            CancellationToken token)
        {
            return client.GetStringWithCancellationAsync(uri, false, token);
        }
        
        public static async Task<string> GetStringWithCancellationAsync(this HttpClient client, 
            Uri uri, 
            bool returnEmptyIfNotFound, 
            CancellationToken token)
        {
            using (var response = await client.GetAsync(uri, token).ConfigureAwait(false))
            {
                if (returnEmptyIfNotFound && response.StatusCode == HttpStatusCode.NotFound)
                    return string.Empty;
                
                response.EnsureSuccessStatusCodeEx();
                HttpContent content = response.Content;
                if (content != null)
                {
                    return await content.ReadAsStringAsync().ConfigureAwait(false);
                }

                return string.Empty;
            }
        }

        public static void EnsureSuccessStatusCodeEx(this HttpResponseMessage message)
        {
            try
            {
                // Call original EnsureSuccessStatusCode to get the same behavior and retrieve the error message.
                message.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new QBittorrentClientRequestException(ex.Message, message.StatusCode);
            }
        }
    }
}
