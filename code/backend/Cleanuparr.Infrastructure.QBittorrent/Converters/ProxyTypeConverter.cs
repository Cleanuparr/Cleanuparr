using System;
using Newtonsoft.Json;

namespace QBittorrent.Client
{
    /// <summary>
    /// JSON converter for the <see cref="ProxyType"/> preference.
    /// </summary>
    public class ProxyTypeConverter : JsonConverter
    {
        private readonly ApiVersion _apiVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyTypeConverter"/> class.
        /// </summary>
        /// <param name="apiVersion">qBittorrent API version.</param>
        public ProxyTypeConverter(ApiVersion apiVersion)
        {
            _apiVersion = apiVersion;
        }

        /// <summary>
        /// Indicates whether this converter can write a type to JSON.
        /// </summary>
        /// <param name="objectType">The type.</param>
        /// <returns>True if <paramref name="objectType"/> is nullable <see cref="ProxyType"/>.</returns>
        public override bool CanConvert(Type objectType) => objectType == typeof(ProxyType?);

        /// <summary>
        /// Indicates whether this converter can read JSON.
        /// </summary>
        /// <returns>Always false.</returns>
        public override bool CanRead => false;

        /// <summary>
        /// Reads a value from JSON.
        /// </summary>
        /// <exception cref="NotImplementedException">This converter is not capable of reading JSON.</exception>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes a value to JSON.
        /// </summary>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var proxyType = (ProxyType?)value;
            if (_apiVersion < new ApiVersion(2, 9))
            {
                writer.WriteValue((int?)proxyType);
                return;
            }

            // from 2.9 authentication is a separate flag, not part of the type
            string name = proxyType switch
            {
                ProxyType.HttpAuth => "HTTP",
                ProxyType.Socks5Auth => "SOCKS5",
                _ => proxyType?.ToString().ToUpper()
            };
            writer.WriteValue(name);
        }
    }
}
