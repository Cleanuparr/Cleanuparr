#nullable enable
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
	/// <summary>
    /// Rename torrent result information
    /// </summary>
	public class RenameTorrentInfo
	{ 
        /// <summary>
        /// The torrent's unique Id.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

		/// <summary>
		/// File path.
		/// </summary>
		[JsonPropertyName("path")]
		public string? Path { get; set; }

		/// <summary>
		/// File name.
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }
	}
}
