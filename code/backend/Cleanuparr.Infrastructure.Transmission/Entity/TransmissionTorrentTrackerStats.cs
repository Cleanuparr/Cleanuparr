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
    /// Torrent tracker stats
    /// </summary>
    public class TransmissionTorrentTrackerStats
    {
        /// <summary>
        /// Announce
        /// </summary>
        [JsonPropertyName("announce")]
        public string? Announce{ get; set; }

        /// <summary>
        /// Announce state
        /// </summary>
        [JsonPropertyName("announceState")]
        public long? AnnounceState{ get; set; }

        /// <summary>
        /// Download count
        /// </summary>
        [JsonPropertyName("downloadCount")]
        public long? DownloadCount{ get; set; }

        /// <summary>
        /// Has announced
        /// </summary>
        [JsonPropertyName("hasAnnounced")]
        public bool? HasAnnounced{ get; set; }

        /// <summary>
        /// Has scraped
        /// </summary>
        [JsonPropertyName("hasScraped")]
        public bool? HasScraped{ get; set; }

        /// <summary>
        /// Host
        /// </summary>
        [JsonPropertyName("host")]
        public string? Host{ get; set; }

        /// <summary>
        /// Is backup
        /// </summary>
        [JsonPropertyName("isBackup")]
        public bool? IsBackup{ get; set; }

        /// <summary>
        /// Last announce peer count
        /// </summary>
        [JsonPropertyName("lastAnnouncePeerCount")]
        public long? LastAnnouncePeerCount{ get; set; }

        /// <summary>
        /// Id
        /// </summary>
        [JsonPropertyName("id")]
        public long? Id{ get; set; }

        /// <summary>
        /// Last announce result 
        /// </summary>
        [JsonPropertyName("lastAnnounceResult")]
        public string? LastAnnounceResult{ get; set; }

        /// <summary>
        /// Last announce succeeded
        /// </summary>
        [JsonPropertyName("lastAnnounceSucceeded")]
        public bool? LastAnnounceSucceeded{ get; set; }

        /// <summary>
        /// Last announce start time
        /// </summary>
        [JsonPropertyName("lastAnnounceStartTime")]
        public long? LastAnnounceStartTime{ get; set; }

        /// <summary>
        /// Last scrape result
        /// </summary>
        [JsonPropertyName("lastScrapeResult")]
        public string? LastScrapeResult{ get; set; }

        /// <summary>
        /// Last announce timed out
        /// </summary>
        [JsonPropertyName("lastAnnounceTimedOut")]
        public bool? LastAnnounceTimedOut{ get; set; }

        /// <summary>
        /// Last announce time
        /// </summary>
        [JsonPropertyName("lastAnnounceTime")]
        public long? LastAnnounceTime{ get; set; }

        /// <summary>
        /// Last scrape scceeded
        /// </summary>
        [JsonPropertyName("lastScrapeSucceeded")]
        public bool? LastScrapeSucceeded{ get; set; }

        /// <summary>
        /// Last scrape start time
        /// </summary>
        [JsonPropertyName("lastScrapeStartTime")]
        public long? LastScrapeStartTime{ get; set; }

        /// <summary>
        /// Last scrape timed out
        /// </summary>
        [JsonPropertyName("lastScrapeTimedOut")]
        public bool? LastScrapeTimedOut{ get; set; }

        /// <summary>
        /// Last scrape time
        /// </summary>
        [JsonPropertyName("lastScrapeTime")]
        public long? LastScrapeTime{ get; set; }

        /// <summary>
        /// Scrape
        /// </summary>
        [JsonPropertyName("scrape")]
        public string? Scrape{ get; set; }

        /// <summary>
        /// Tier
        /// </summary>
        [JsonPropertyName("tier")]
        public long? Tier{ get; set; }

        /// <summary>
        /// Leecher count
        /// </summary>
        [JsonPropertyName("leecherCount")]
        public long? LeecherCount{ get; set; }

        /// <summary>
        /// Next announce time
        /// </summary>
        [JsonPropertyName("nextAnnounceTime")]
        public long? NextAnnounceTime{ get; set; }

        /// <summary>
        /// Next scrape time
        /// </summary>
        [JsonPropertyName("nextScrapeTime")]
        public long? NextScrapeTime{ get; set; }

        /// <summary>
        /// Scrape state
        /// </summary>
        [JsonPropertyName("scrapeState")]
        public long? ScrapeState{ get; set; }

        /// <summary>
        /// Seeder count
        /// </summary>
        [JsonPropertyName("seederCount")]
        public long? SeederCount{ get; set; }

        /// <summary>
        /// Site name
        /// </summary>
        [JsonPropertyName("sitename")]
        public string? SiteName { get; set; }
    }
}
