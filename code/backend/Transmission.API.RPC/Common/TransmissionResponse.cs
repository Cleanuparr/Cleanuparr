using System.Text.Json;
using System.Text.Json.Serialization;

namespace Transmission.API.RPC.Common
{
	/// <summary>
	/// Transmission response
	/// </summary>
	public class TransmissionResponse
	{
		/// <summary>
		/// Contains "success" on success, or an error string on failure.
		/// </summary>
        [JsonPropertyName("result")]
        public string Result { get; set; }

        /// <summary>
        /// Uniquely identifies which request this is a response to
        /// </summary>
        [JsonPropertyName("tag")]
        public long? Tag { get; set; }

        /// <summary>
        /// Data
        /// </summary>
        [JsonPropertyName("arguments")]
        public JsonElement Arguments { get; set; }
    }
}
