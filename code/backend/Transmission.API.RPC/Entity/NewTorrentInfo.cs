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
	/// Information of added torrent
	/// </summary>
	public class NewTorrentInfo
	{
		/// <summary>
		/// Torrent ID
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// Torrent name
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		/// <summary>
		/// Torrent Hash
		/// </summary>
		[JsonPropertyName("hashString")]
		public string? HashString { get; set; }

        /// <summary>
        /// Whether the torrent is a duplicate of an existing torrent (add failed)
        /// </summary>
        public bool? Duplicate { get; set; }

	}
}
