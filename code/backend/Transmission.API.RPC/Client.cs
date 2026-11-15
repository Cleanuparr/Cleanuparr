using System;
using System.Text;
using Transmission.API.RPC.Entity;
using Transmission.API.RPC.Arguments;
using System.Net.Http;
using System.Text.Json;

namespace Transmission.API.RPC
{
    /// <summary>
    /// Transmission client
    /// </summary>
    public partial class Client : ITransmissionClientAsync
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new NumberToBoolConverter() }
        };

        private readonly HttpClient _httpClient;
        private readonly string _authorization;
        private readonly bool _needAuthorization;

        /// <summary>
        /// Url to service
        /// </summary>
        public string Url
        {
            get;
            private set;
        }

        /// <summary>
        /// Session ID
        /// </summary>
        public string SessionID
        {
            get;
            private set;
        }

        /// <summary>
        /// Current Tag
        /// </summary>
        public long CurrentTag
        {
            get;
            private set;
        }

        /// <summary>
        /// Initialize client
        /// <example>For example
        /// <code>
        /// new Transmission.API.RPC.Client("https://website.com:9091/transmission/rpc")
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="url">URL to Transmission RPC API. Often it looks like schema://host:port/transmission/rpc </param>
        /// <param name="sessionID">Session ID</param>
        /// <param name="login">Login</param>
        /// <param name="password">Password</param>
        /// <param name="httpClient">HttpClient</param>
        public Client(HttpClient httpClient, string url, string sessionID = null, string login = null, string password = null)
        {
            this.Url = url;
            this.SessionID = sessionID;
            _httpClient = httpClient;

            if (!String.IsNullOrWhiteSpace(login))
            {
                var authBytes = Encoding.UTF8.GetBytes(login + ":" + password);
                var encoded = Convert.ToBase64String(authBytes);

                this._authorization = "Basic " + encoded;
                this._needAuthorization = true;
            }
        }
    }
}
