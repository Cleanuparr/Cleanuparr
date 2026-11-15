using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// qBittorrent application preferences.
    /// </summary>
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class Preferences
    {
        private const string WebUiPasswordPropertyName = "web_ui_password";
        private const string AddTorrentStoppedPropertyName = "add_stopped_enabled";

        /// <summary>
        /// Currently selected language
        /// </summary>
        [JsonProperty("locale")]
        public string Locale { get; set; }

        /// <summary>
        /// Default save path for torrents, separated by slashes
        /// </summary>
        [JsonProperty("save_path")]
        public string SavePath { get; set; }

        /// <summary>
        /// True if folder for incomplete torrents is enabled
        /// </summary>
        [JsonProperty("temp_path_enabled")]
        public bool? TempPathEnabled { get; set; }

        /// <summary>
        /// Path for incomplete torrents, separated by slashes
        /// </summary>
        [JsonProperty("temp_path")]
        public string TempPath { get; set; }

        /// <summary>
        /// List of watch folders to add torrent automatically.
        /// </summary>
        [JsonProperty("scan_dirs")]
        public IDictionary<string, SaveLocation> ScanDirectories { get; set; }

        /// <summary>
        /// Path to directory to copy .torrent files.
        /// </summary>
        [JsonProperty("export_dir")]
        public string ExportDirectory { get; set; }

        /// <summary>
        /// Path to directory to copy finished .torrent files
        /// </summary>
        [JsonProperty("export_dir_fin")]
        public string ExportDirectoryForFinished { get; set; }

        /// <summary>
        /// True if e-mail notification should be enabled.
        /// </summary>
        [JsonProperty("mail_notification_enabled")]
        public bool? MailNotificationEnabled { get; set; }

        /// <summary>
        /// E-mail address to send notifications to.
        /// </summary>
        [JsonProperty("mail_notification_email")]
        public string MailNotificationEmailAddress { get; set; }

        /// <summary>
        /// SMTP server for e-mail notifications.
        /// </summary>
        [JsonProperty("mail_notification_smtp")]
        public string MailNotificationSmtpServer { get; set; }

        /// <summary>
        /// True if SMTP server requires SSL connection.
        /// </summary>
        [JsonProperty("mail_notification_ssl_enabled")]
        public bool? MailNotificationSslEnabled { get; set; }

        /// <summary>
        /// True if SMTP server requires authentication
        /// </summary>
        [JsonProperty("mail_notification_auth_enabled")]
        public bool? MailNotificationAuthenticationEnabled { get; set; }

        /// <summary>
        /// Username for SMTP authentication.
        /// </summary>
        [JsonProperty("mail_notification_username")]
        public string MailNotificationUsername { get; set; }

        /// <summary>
        /// Password for SMTP authentication.
        /// </summary>
        [JsonProperty("mail_notification_password")]
        public string MailNotificationPassword { get; set; }

        /// <summary>
        /// True if external program should be run after torrent has finished downloading.
        /// </summary>
        [JsonProperty("autorun_enabled")]
        public bool? AutorunEnabled { get; set; }

        /// <summary>
        /// Program path/name/arguments to run if <see cref="AutorunEnabled"/> is <see langword="true"/>.
        /// </summary>
        [JsonProperty("autorun_program")]
        public string AutorunProgram { get; set; }

        /// <summary>
        /// True if file preallocation should take place, otherwise sparse files are used.
        /// </summary>
        [JsonProperty("preallocate_all")]
        public bool? PreallocateAll { get; set; }

        /// <summary>
        /// True if torrent queuing is enabled
        /// </summary>
        [JsonProperty("queueing_enabled")]
        public bool? QueueingEnabled { get; set; }

        /// <summary>
        /// Maximum number of active simultaneous downloads
        /// </summary>
        [JsonProperty("max_active_downloads")]
        public int? MaxActiveDownloads { get; set; }

        /// <summary>
        /// Maximum number of active simultaneous downloads and uploads
        /// </summary>
        [JsonProperty("max_active_torrents")]
        public int? MaxActiveTorrents { get; set; }

        /// <summary>
        /// Maximum number of active simultaneous uploads
        /// </summary>
        [JsonProperty("max_active_uploads")]
        public int? MaxActiveUploads { get; set; }

        /// <summary>
        /// If true torrents w/o any activity (stalled ones) will not be counted towards max_active_* limits.
        /// </summary>
        [JsonProperty("dont_count_slow_torrents")]
        public bool? DoNotCountSlowTorrents { get; set; }

        /// <summary>
        /// True if share ratio limit is enabled
        /// </summary>
        [JsonProperty("max_ratio_enabled")]
        public bool? MaxRatioEnabled { get; set; }

        /// <summary>
        /// Get the global share ratio limit
        /// </summary>
        [JsonProperty("max_ratio")]
        public double? MaxRatio { get; set; }

        /// <summary>
        /// Action performed when a torrent reaches the maximum share ratio.
        /// </summary>
        [JsonProperty("max_ratio_act")]
        public MaxRatioAction? MaxRatioAction { get; set; }

        /// <summary>
        /// Maximal seeding time in minutes.
        /// </summary>
        [JsonProperty("max_seeding_time")]
        public int? MaxSeedingTime { get; set; }

        /// <summary>
        /// True if maximal seeding time is enabled.
        /// </summary>
        [JsonProperty("max_seeding_time_enabled")]
        public bool? MaxSeedingTimeEnabled { get; set; }
        
        /// <summary>
        /// Maximal inactive seeding time in minutes.
        /// </summary>
        [JsonProperty("max_inactive_seeding_time")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.9.2")]
        public int? MaxInactiveSeedingTime { get; set; }

        /// <summary>
        /// True if maximal inactive seeding time is enabled.
        /// </summary>
        [JsonProperty("max_inactive_seeding_time_enabled")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.9.2")]
        public bool? MaxInactiveSeedingTimeEnabled { get; set; }

        /// <summary>
        /// If true <c>.!qB</c> extension will be appended to incomplete files.
        /// </summary>
        [JsonProperty("incomplete_files_ext")]
        public bool? AppendExtensionToIncompleteFiles { get; set; }

        /// <summary>
        /// Port for incoming connections.
        /// </summary>
        [JsonProperty("listen_port")]
        public int? ListenPort { get; set; }

        /// <summary>
        /// True if UPnP/NAT-PMP is enabled.
        /// </summary>
        [JsonProperty("upnp")]
        public bool? UpnpEnabled { get; set; }

        /// <summary>
        /// True if the port is randomly selected
        /// </summary>
        [JsonProperty("random_port")]
        public bool? RandomPort { get; set; }

        /// <summary>
        /// Global download speed limit in KiB/s; -1 means no limit is applied.
        /// </summary>
        [JsonProperty("dl_limit")]
        public int? DownloadLimit { get; set; }

        /// <summary>
        /// Global upload speed limit in KiB/s; -1 means no limit is applied.
        /// </summary>
        [JsonProperty("up_limit")]
        public int? UploadLimit { get; set; }

        /// <summary>
        /// Maximum global number of simultaneous connections.
        /// </summary>
        [JsonProperty("max_connec")]
        public int? MaxConnections { get; set; }

        /// <summary>
        /// Maximum number of simultaneous connections per torrent.
        /// </summary>
        [JsonProperty("max_connec_per_torrent")]
        public int? MaxConnectionsPerTorrent { get; set; }

        /// <summary>
        /// Maximum number of upload slots.
        /// </summary>
        [JsonProperty("max_uploads")]
        public int? MaxUploads { get; set; }

        /// <summary>
        /// Maximum number of upload slots per torrent
        /// </summary>
        [JsonProperty("max_uploads_per_torrent")]
        public int? MaxUploadsPerTorrent { get; set; }

        /// <summary>
        /// True if uTP protocol should be enabled.
        /// </summary>
        [JsonProperty("enable_utp")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [Obsolete("Use BittorrentProtocol property for qBittorrent 4.x")]
        public bool? EnableUTP { get; set; }

        /// <summary>
        /// True if <see cref="DownloadLimit"/> and <see cref="UploadLimit"/> should be applied to uTP connections.
        /// </summary>
        [JsonProperty("limit_utp_rate")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? LimitUTPRate { get; set; }

        /// <summary>
        /// True if <see cref="DownloadLimit"/> and <see cref="UploadLimit"/>
        /// should be applied to estimated TCP overhead (service data: e.g. packet headers).
        /// </summary>
        [JsonProperty("limit_tcp_overhead")]
        public bool? LimitTcpOverhead { get; set; }

        /// <summary>
        /// Alternative global download speed limit in KiB/s
        /// </summary>
        [JsonProperty("alt_dl_limit")]
        public int? AlternativeDownloadLimit { get; set; }

        /// <summary>
        /// Alternative global upload speed limit in KiB/s
        /// </summary>
        [JsonProperty("alt_up_limit")]
        public int? AlternativeUploadLimit { get; set; }

        /// <summary>
        /// True if alternative limits should be applied according to schedule
        /// </summary>
        [JsonProperty("scheduler_enabled")]
        public bool? SchedulerEnabled { get; set; }

        /// <summary>
        /// Scheduler starting hour.
        /// </summary>
        [JsonProperty("schedule_from_hour")]
        public int? ScheduleFromHour { get; set; }

        /// <summary>
        /// Scheduler starting minute.
        /// </summary>
        [JsonProperty("schedule_from_min")]
        public int? ScheduleFromMinute { get; set; }

        /// <summary>
        /// Scheduler ending hour.
        /// </summary>
        [JsonProperty("schedule_to_hour")]
        public int? ScheduleToHour { get; set; }

        /// <summary>
        /// Scheduler ending minute.
        /// </summary>
        [JsonProperty("schedule_to_min")]
        public int? ScheduleToMinute { get; set; }

        /// <summary>
        /// Scheduler days.
        /// </summary>
        [JsonProperty("scheduler_days")]
        public SchedulerDay? SchedulerDays { get; set; }

        /// <summary>
        /// True if DHT is enabled
        /// </summary>
        [JsonProperty("dht")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? DHT { get; set; }

        /// <summary>
        /// True if DHT port should match TCP port
        /// </summary>
        [JsonProperty("dhtSameAsBT")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? DHTSameAsBT { get; set; }

        /// <summary>
        /// DHT port if <see cref="DHTSameAsBT"/> is <see langword="false"/>.
        /// </summary>
        [JsonProperty("dht_port")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public int? DHTPort { get; set; }

        /// <summary>
        /// True if peer exchange PeX is enabled.
        /// </summary>
        [JsonProperty("pex")]
        public bool? PeerExchange { get; set; }

        /// <summary>
        /// True if local peer discovery is enabled
        /// </summary>
        [JsonProperty("lsd")]
        public bool? LocalPeerDiscovery { get; set; }

        /// <summary>
        /// Encryption mode.
        /// </summary>
        [JsonProperty("encryption")]
        public Encryption? Encryption { get; set; }

        /// <summary>
        /// If true anonymous mode will be enabled.
        /// </summary>
        [JsonProperty("anonymous_mode")]
        public bool? AnonymousMode { get; set; }

        /// <summary>
        /// Proxy type.
        /// </summary>
        [JsonProperty("proxy_type")]
        public ProxyType? ProxyType { get; set; }

        /// <summary>
        /// Proxy IP address or domain name.
        /// </summary>
        [JsonProperty("proxy_ip")]
        public string ProxyAddress { get; set; }

        /// <summary>
        /// Proxy port.
        /// </summary>
        [JsonProperty("proxy_port")]
        public int? ProxyPort { get; set; }

        /// <summary>
        /// True if peer and web seed connections should be proxified.
        /// </summary>
        [JsonProperty("proxy_peer_connections")]
        public bool? ProxyPeerConnections { get; set; }

        /// <summary>
        /// True if the connections not supported by the proxy are disabled.
        /// </summary>
        [JsonProperty("force_proxy")]
        [Deprecated("2.3")]
        public bool? ForceProxy { get; set; }

        /// <summary>
        /// True if proxy should be used only for torrents.
        /// </summary>
        [JsonProperty("proxy_torrents_only")]
        [ApiLevel(ApiLevel.V2, MaxVersion = "2.8.19")]
        public bool? ProxyTorrentsOnly { get; set; }

        /// <summary>
        /// True if proxy requires authentication; doesn't apply to SOCKS4 proxies.
        /// </summary>
        [JsonProperty("proxy_auth_enabled")]
        [Obsolete("Use ProxyType instead.")]
        public bool? ProxyAuthenticationEnabled { get; set; }

        /// <summary>
        /// Username for proxy authentication.
        /// </summary>
        [JsonProperty("proxy_username")]
        public string ProxyUsername { get; set; }

        /// <summary>
        /// Password for proxy authentication.
        /// </summary>
        [JsonProperty("proxy_password")]
        public string ProxyPassword { get; set; }

        /// <summary>
        /// True if proxy should be used to perform hostname lookup.
        /// </summary>
        [JsonProperty("proxy_hostname_lookup")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.8.18")]
        public bool? ProxyHostnameLookup { get; set; }

        /// <summary>
        /// True if proxy should be used for BitTorrent purposes.
        /// </summary>
        [JsonProperty("proxy_bittorrent")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.9.1")]
        public bool? ProxyBittorrent { get; set; }

        /// <summary>
        /// True if proxy should be used for general purposes.
        /// </summary>
        [JsonProperty("proxy_misc")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.9.1")]
        public bool? ProxyMisc { get; set; }

        /// <summary>
        /// True if proxy should be used for RSS purposes.
        /// </summary>
        [JsonProperty("proxy_rss")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.9.1")]
        public bool? ProxyRss { get; set; }

        /// <summary>
        /// True if external IP filter should be enabled.
        /// </summary>
        [JsonProperty("ip_filter_enabled")]
        public bool? IpFilterEnabled { get; set; }

        /// <summary>
        /// Path to IP filter file (.dat, .p2p, .p2b files are supported).
        /// </summary>
        [JsonProperty("ip_filter_path")]
        public string IpFilterPath { get; set; }

        /// <summary>
        /// True if IP filters are applied to trackers
        /// </summary>
        [JsonProperty("ip_filter_trackers")]
        public bool? IpFilterTrackers { get; set; }

        /// <summary>
        /// WebUI IP address. Use <c>*</c> to accept connections on any IP address.
        /// </summary>
        [JsonProperty("web_ui_address")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public string WebUIAddress { get; set; }

        /// <summary>
        /// WebUI port.
        /// </summary>
        [JsonProperty("web_ui_port")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public int? WebUIPort { get; set; }

        /// <summary>
        /// WebUI domain. Use <c>*</c> to accept connections on any domain.
        /// </summary>
        [JsonProperty("web_ui_domain_list")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public string WebUIDomain { get; set; }

        /// <summary>
        /// True if UPnP is used for the WebUI port.
        /// </summary>
        [JsonProperty("web_ui_upnp")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? WebUIUpnp { get; set; }

        /// <summary>
        /// WebUI username
        /// </summary>
        [JsonProperty("web_ui_username")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public string WebUIUsername { get; set; }

        /// <summary>
        /// WebUI password. 
        /// </summary>
        /// <remarks>
        /// This property should be used for setting password.
        /// If a <see cref="Preferences"/> object is retrieved as server response, this property will be <see langword="null"/>.
        /// </remarks>
        [JsonIgnore]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public string WebUIPassword { get; set; }

        /// <summary>
        /// MD5 hash of WebUI password. 
        /// </summary>
        /// <remarks>
        /// This property can be used to get the MD5 hash of the current WebUI password.
        /// It is ignored when sending requests to the server.
        /// </remarks>
        [JsonIgnore]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public string WebUIPasswordHash { get; set; }

        /// <summary>
        /// True if WebUI HTTPS access is enabled.
        /// </summary>
        [JsonProperty("use_https")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? WebUIHttps { get; set; }

        /// <summary>
        /// SSL keyfile contents (this is a not a path).
        /// </summary>
        [JsonProperty("ssl_key")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [Deprecated("2.3", Description = "Use WebUISslKeyPath on API 2.3 or later.")]
        public string WebUISslKey { get; set; }

        /// <summary>
        /// SSL certificate contents (this is a not a path).
        /// </summary>
        [JsonProperty("ssl_cert")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [Deprecated("2.3", Description = "Use WebUISslCertificatePath on API 2.3 or later.")]
        public string WebUISslCertificate { get; set; }

        /// <summary>
        /// True if WebUI clickjacking protection is enabled
        /// </summary>
        [JsonProperty("web_ui_clickjacking_protection_enabled")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.2")]
        public bool? WebUIClickjackingProtection { get; set; }

        /// <summary>
        /// True if WebUI CSRF protection is enabled
        /// </summary>
        [JsonProperty("web_ui_csrf_protection_enabled")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.2")]
        public bool? WebUICsrfProtection { get; set; }

        /// <summary>
        /// True if Secure attribute is set on cookie when using HTTPS
        /// </summary>
        [JsonProperty("web_ui_secure_cookie_enabled")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        public bool? WebUISecureCookie { get; set; }
        
        /// <summary>
        /// The number of the failed authentication attempt after which the client will be banned.
        /// </summary>
        /// <seealso cref="WebUIBanDuration"/>
        [JsonProperty("web_ui_max_auth_fail_count")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        public int? WebUIMaxAuthenticationFailures { get; set; }

        /// <summary>
        /// The duration (in seconds) the client will be banned for after <see cref="WebUIMaxAuthenticationFailures"/> failed authentication attempts.
        /// </summary>
        [JsonProperty("web_ui_ban_duration")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        public int? WebUIBanDuration { get; set; }

        /// <summary>
        /// True if custom HTTP headers are enabled for Web UI.
        /// </summary>
        [JsonProperty("web_ui_use_custom_http_headers_enabled")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        public bool? WebUICustomHttpHeadersEnabled { get; set; }

        /// <summary>
        /// Custom HTTP headers to be used for Web UI.
        /// </summary>
        /// <remarks>
        /// Each header must be specified as a key-value pair separated by a colon, i.e.<c>HEADER:VALUE</c>. The headers must be separated by a new-line character (<c>\n</c>).
        /// </remarks>
        [JsonProperty("web_ui_custom_http_headers")]
        [JsonConverter(typeof(SeparatedStringToListConverter), "\n")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        public IList<string> WebUICustomHttpHeaders { get; set; }

        /// <summary>
        /// True if authentication challenge for loopback address (127.0.0.1) should be disabled.
        /// </summary>
        [JsonProperty("bypass_local_auth")]
        public bool? BypassLocalAuthentication { get; set; }

        /// <summary>
        /// True if webui authentication should be bypassed for clients whose ip resides within (at least) one of the subnets on the whitelist.
        /// </summary>
        /// <seealso cref="BypassAuthenticationSubnetWhitelist"/>
        /// <seealso cref="BypassLocalAuthentication"/>
        [JsonProperty("bypass_auth_subnet_whitelist_enabled")]
        public bool? BypassAuthenticationSubnetWhitelistEnabled { get; set; }

        /// <summary>
        /// (White)list of ipv4/ipv6 subnets for which webui authentication should be bypassed.
        /// </summary>
        /// <seealso cref="BypassAuthenticationSubnetWhitelistEnabled"/>
        /// <seealso cref="BypassLocalAuthentication"/>
        [JsonProperty("bypass_auth_subnet_whitelist")]
        [JsonConverter(typeof(SeparatedStringToListConverter), ",", "\n")]
        public IList<string> BypassAuthenticationSubnetWhitelist { get; set; }

        /// <summary>
        /// True if server DNS should be updated dynamically.
        /// </summary>
        [JsonProperty("dyndns_enabled")]
        public bool? DynamicDnsEnabled { get; set; }

        /// <summary>
        /// Dynamic DNS service.
        /// </summary>
        [JsonProperty("dyndns_service")]
        public DynamicDnsService? DynamicDnsService { get; set; }

        /// <summary>
        /// Username for DDNS service.
        /// </summary>
        [JsonProperty("dyndns_username")]
        public string DynamicDnsUsername { get; set; }

        /// <summary>
        /// Password for DDNS service.
        /// </summary>
        [JsonProperty("dyndns_password")]
        public string DynamicDnsPassword { get; set; }

        /// <summary>
        /// Your DDNS domain name.
        /// </summary>
        [JsonProperty("dyndns_domain")]
        public string DynamicDnsDomain { get; set; }

        /// <summary>
        /// RSS refresh interval.
        /// </summary>
        [JsonProperty("rss_refresh_interval")]
        public uint? RssRefreshInterval { get; set; }

        /// <summary>
        /// Max stored articles per RSS feed.
        /// </summary>
        [JsonProperty("rss_max_articles_per_feed")]
        public int? RssMaxArticlesPerFeed { get; set; }

        /// <summary>
        /// Enable processing of RSS feeds.
        /// </summary>
        [JsonProperty("rss_processing_enabled")]
        public bool? RssProcessingEnabled { get; set; }

        /// <summary>
        /// Enable auto-downloading of torrents from the RSS feeds.
        /// </summary>
        [JsonProperty("rss_auto_downloading_enabled")]
        public bool? RssAutoDownloadingEnabled { get; set; }

        /// <summary>
        /// Enables downloading REPACK/PROPER episodes.
        /// </summary>
        [JsonProperty("rss_download_repack_proper_episodes")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        public bool? RssDownloadRepackProperEpisodes { get; set; }

        /// <summary>
        /// Smart episode filters.
        /// </summary>
        [JsonProperty("rss_smart_episode_filters")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        [JsonConverter(typeof(SeparatedStringToListConverter), "\n")]
        public IList<string> RssSmartEpisodeFilters { get; set; }

        /// <summary>
        /// True if additional trackers are enabled.
        /// </summary>
        [JsonProperty("add_trackers_enabled")]
        public bool? AdditionalTrackersEnabled { get; set; }

        /// <summary>
        /// The list of addional trackers.
        /// </summary>
        [JsonProperty("add_trackers")]
        [JsonConverter(typeof(SeparatedStringToListConverter), "\n")]
        public IList<string> AdditinalTrackers { get; set; }

        /// <summary>
        /// The list of banned IP addresses.
        /// </summary>
        [JsonProperty("banned_IPs")]
        [JsonConverter(typeof(SeparatedStringToListConverter), "\n")]
        public IList<string> BannedIpAddresses { get; set; }

        /// <summary>
        /// Bittorrent protocol.
        /// </summary>
        [JsonProperty("bittorrent_protocol")]
        public BittorrentProtocol? BittorrentProtocol { get; set; }

        /* API 2.2.0 */

        /// <summary>
        /// True if a subfolder should be created when adding a torrent
        /// </summary>
        [JsonProperty("create_subfolder_enabled")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        [Deprecated("2.7.0")]
        public bool? CreateTorrentSubfolder { get; set; }

        /// <summary>
        /// True if torrents should be added in a Paused state
        /// </summary>
        [JsonProperty("start_paused_enabled")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public bool? AddTorrentPaused { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("auto_delete_mode")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public TorrentFileAutoDeleteMode? TorrentFileAutoDeleteMode { get; set; }

        /// <summary>
        /// True if Automatic Torrent Management is enabled by default
        /// </summary>
        [JsonProperty("auto_tmm_enabled")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? AutoTMMEnabledByDefault { get; set; }

        /// <summary>
        /// True if torrent should be relocated when its category changes
        /// </summary>
        [JsonProperty("torrent_changed_tmm_enabled")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? AutoTMMRetainedWhenCategoryChanges { get; set; }

        /// <summary>
        /// True if torrent should be relocated when the default save path changes
        /// </summary>
        [JsonProperty("save_path_changed_tmm_enabled")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? AutoTMMRetainedWhenDefaultSavePathChanges { get; set; }


        /// <summary>
        /// True if torrent should be relocated when its category's save path changes
        /// </summary>
        [JsonProperty("category_changed_tmm_enabled")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? AutoTMMRetainedWhenCategorySavePathChanges { get; set; }

        /// <summary>
        /// E-mail where notifications should originate from
        /// </summary>
        [JsonProperty("mail_notification_sender")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public string MailNotificationSender { get; set; }

        /// <summary>
        /// True if <see cref="DownloadLimit" /> and <seealso cref="UploadLimit"/> should be applied to peers on the LAN
        /// </summary>
        [JsonProperty("limit_lan_peers")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public bool? LimitLAN { get; set; }

        /// <summary>
        /// Download rate in KiB/s for a torrent to be considered "slow"
        /// </summary>
        [JsonProperty("slow_torrent_dl_rate_threshold")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public int? SlowTorrentDownloadRateThreshold { get; set; }

        /// <summary>
        /// Upload rate in KiB/s for a torrent to be considered "slow"
        /// </summary>
        [JsonProperty("slow_torrent_ul_rate_threshold")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public int? SlowTorrentUploadRateThreshold { get; set; }

        /// <summary>
        /// Time in seconds a torrent should be inactive before considered "slow"
        /// </summary>
        [JsonProperty("slow_torrent_inactive_timer")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public int? SlowTorrentInactiveTime { get; set; }

        /// <summary>
        /// True if an alternative WebUI should be used
        /// </summary>
        [JsonProperty("alternative_webui_enabled")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public bool? AlternativeWebUIEnabled { get; set; }

        /// <summary>
        /// File path to the alternative WebUI
        /// </summary>
        [JsonProperty("alternative_webui_path")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public string AlternativeWebUIPath { get; set; }

        /// <summary>
        /// True if WebUI host header validation is enabled
        /// </summary>
        [JsonProperty("web_ui_host_header_validation_enabled")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2")]
        public bool? WebUIHostHeaderValidation { get; set; }

        /* API 2.3.0 */

        /// <summary>
        /// SSL key file path on the server.
        /// </summary>
        [JsonProperty("web_ui_https_key_path")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public string WebUISslKeyPath { get; set; }

        /// <summary>
        /// SSL certificate file path on the server.
        /// </summary>
        [JsonProperty("web_ui_https_cert_path")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public string WebUISslCertificatePath { get; set; }

        /// <summary>
        /// Web UI session timeout in seconds
        /// </summary>
        [JsonProperty("web_ui_session_timeout")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? WebUISessionTimeout { get; set; }

        /// <summary>
        /// Specifies which network interfaces qBittorrent listens on.
        /// On multi-nic systems (e.g. device has an ethernet port and wifi)
        /// you may limit which interface should be used to send and transmit data.
        /// </summary>
        [JsonProperty("current_network_interface")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public string CurrentNetworkInterface { get; set; }

        /// <summary>
        /// Current network interface address
        /// </summary>
        [JsonProperty("current_interface_address")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public string CurrentInterfaceAddress { get; set; }

        /// <summary>
        /// Allows qBittorrent to listen on IPv6 address in addition to the old, standard IPv4.
        /// </summary>
        [JsonProperty("listen_on_ipv6_address")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        [Deprecated("2.4")]
        public bool? ListenOnIPv6Address { get; set; }

        /// <summary>
        /// Duration in minutes on which the resume data is saved to the disk.
        /// </summary>
        [JsonProperty("save_resume_data_interval")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? SaveResumeDataInterval { get; set; }

        /// <summary>
        /// When enabled, after the torrent is completely downloaded a recheck operation is performed on the torrent.
        /// </summary>
        [JsonProperty("recheck_completed_torrents")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? RecheckCompletedTorrents { get; set; }

        /// <summary>
        /// When enabled, qBittorrent tries to lookup the originating country of each peer using GeoIP database.
        /// </summary>
        [JsonProperty("resolve_peer_countries")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? ResolvePeerCountries { get; set; }

        /// <summary>
        /// I/O threads that <c>libtorrent</c> will use.
        /// </summary>
        /// <remarks>
        /// The number of threads actually used for SHA-1 hashing is n/4 (where n is the value of the setting),
        /// so for maximum performance, especially during torrent recheck,
        /// this setting should be set to 4 times the number of hardware threads on your machine.
        /// So for example, if your CPU is 4c/4t or 2c/4t, set this to 16, if your CPU is 4c/8t or 8c/8t set this to 32, etc.
        /// It is unlikely that setting this any higher than this will bring a performance benefit.
        /// </remarks>
        [JsonProperty("async_io_threads")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentAsynchronousIOThreads { get; set; }

        /// <summary>
        /// The upper limit on the total number of files the session will keep open.
        /// </summary>
        /// <remarks>
        /// The reason why files are left open at all is that some anti virus software
        /// hooks on every file close, and scans the file for viruses.
        /// Deferring the closing of the files will be the difference between a usable system and a completely hogged down system.
        /// Most operating systems also has a limit on the total number of file descriptors a process may have open.
        /// </remarks>
        [JsonProperty("file_pool_size")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentFilePoolSize { get; set; }

        /// <summary>
        /// The amount of memory (in MiB) to use when checking torrents. Higher numbers give faster rechecks but use more memory.
        /// </summary>
        [JsonProperty("checking_memory_use")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentOutstandingMemoryWhenCheckingTorrent { get; set; }

        /// <summary>
        /// Amount of data (in MiB) that will remain in RAM before being written to disk.
        /// If set to <c>0</c>, no data will be kept in RAM and instead it will be immediately written to disk (you might see performance impact.)
        /// </summary>
        /// <seealso cref="LibtorrentDiskCacheExpiryInterval"/>
        [JsonProperty("disk_cache")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentDiskCache { get; set; }

        /// <summary>
        /// The number of seconds from the last cached write to a piece in the write cache, to when it's forcefully flushed to disk.
        /// </summary>
        /// <seealso cref="LibtorrentDiskCache"/>
        [JsonProperty("disk_cache_ttl")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentDiskCacheExpiryInterval { get; set; }

        /// <summary>
        /// When enabled, files are opened normally, with the OS caching reads and writes.
        /// </summary>
        /// <remarks>
        /// Enable for better performance, disable if you also disabled libtorrent's read cache,
        /// or to help preventing the operating system from growing its file cache indefinitely,
        /// or if you want to prevent qbittorrent from potentially evicting all other processes' cache
        /// (which may result in lower perceived system responsiveness).
        /// </remarks>
        [JsonProperty("enable_os_cache")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3", MaxVersion = "2.8.19")]
        public bool? LibtorrentUseOSCache { get; set; }

        /// <summary>
        /// When enabled, qbittorrent will allocate separate, contiguous, buffers for read and write calls.
        /// </summary>
        /// <remarks>
        /// Only used where writev/readv cannot be used, and will use more RAM but may improve performance.
        /// </remarks>
        [JsonProperty("enable_coalesce_read_write")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? LibtorrentCoalesceReadsAndWrites { get; set; }

        /// <summary>
        /// When this is <see langword="true" />, create an affinity for downloading 4 MiB extents of adjacent pieces.
        /// </summary>
        /// <remarks>
        /// This is an attempt to achieve better disk I/O throughput by downloading larger extents of bytes, for torrents with small piece sizes.
        /// </remarks>
        [JsonProperty("enable_piece_extent_affinity")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        public bool? LibtorrentPieceExtentAffinity { get; set; }

        /// <summary>
        /// Controls whether or not libtorrent will send out suggest messages to create a bias of its peers to request certain pieces.
        /// If enabled, libtorrent will send out suggest messages for the most recent pieces that are in the read cache.
        /// </summary>
        [JsonProperty("enable_upload_suggestions")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? LibtorrentSendUploadPieceSuggestions { get; set; }

        /// <summary>
        /// The upper limit for the send buffer size in kiB.
        /// </summary>
        /// <remarks>
        /// If the send buffer has fewer bytes than this, another 16kiB block will be read into it.
        /// If set too small, upload rate capacity will suffer. If set too high, memory will be wasted.
        /// The actual watermark may be lower than this in case the upload rate is low.
        /// </remarks>
        /// <seealso cref="LibtorrentSendBufferLowWatermark"/>
        /// <seealso cref="LibtorrentSendBufferWatermarkFactor"/>
        [JsonProperty("send_buffer_watermark")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentSendBufferWatermark { get; set; }

        /// <summary>
        /// The minimum send buffer target size in kiB.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Send buffer includes bytes pending being read from disk.
        /// </para>
        /// <para>
        /// For good and snappy seeding performance, set this fairly high, to at least fit a few blocks.
        /// This is essentially the initial window size which will determine how fast we can ramp up the send rate.
        /// </para>
        /// </remarks>
        /// <seealso cref="LibtorrentSendBufferWatermark"/>
        /// <seealso cref="LibtorrentSendBufferWatermarkFactor"/>
        [JsonProperty("send_buffer_low_watermark")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentSendBufferLowWatermark { get; set; }

        /// <summary>
        /// The send buffer watermark factor in percent.
        /// </summary>
        /// <remarks>
        /// The current upload rate to a peer is multiplied by this factor (in percent) to get the send buffer watermark.
        /// This product is clamped to the <see cref="LibtorrentSendBufferWatermark"/> setting so as to not exceed the max.
        /// For high speed upload, this should be set to a greater value than 100.
        /// For high capacity connections, setting this higher can improve upload performance and disk throughput.
        /// Setting it too high may waste RAM and create a bias towards read jobs over write jobs.
        /// </remarks>
        /// <seealso cref="LibtorrentSendBufferWatermark"/>
        /// <seealso cref="LibtorrentSendBufferLowWatermark"/>
        [JsonProperty("send_buffer_watermark_factor")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentSendBufferWatermarkFactor { get; set; }

        /// <summary>
        /// The number of outstanding incoming connections to queue up while we're not actively waiting for a connection to be accepted.
        /// </summary>
        /// <remarks>
        /// The default is 5 which should be sufficient for any normal client.
        /// If this is a high performance server which expects to receive a lot of connections,
        /// or used in a simulator or test, it might make sense to raise this number.
        /// </remarks>
        [JsonProperty("socket_backlog_size")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentSocketBacklogSize { get; set; }


        /// <summary>
        /// The start of the range of ports to use for binding outgoing connections to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property together with <see cref="LibtorrentOutgoingPortsMax"/> defines the range of ports to use for binding outgoing connections to.
        /// The range shouldn't be too small.
        /// </para>
        /// <para>
        /// The value of <c>0</c> disables this setting.
        /// </para>
        /// <para>
        /// Setting outgoing ports will limit the ability to keep multiple connections to the same client, even for different torrents.
        /// It is not recommended to change this setting.
        /// Its main purpose is to use it as an escape hatch for cheap routers with QoS capability but can only classify flows based on port numbers.
        /// It is a range instead of a single port because of the problems with failing to reconnect to peers
        /// if a previous socket to that peer and port is in the waiting state.
        /// </para>
        /// </remarks>
        /// <seealso cref="LibtorrentOutgoingPortsMax"/>
        [JsonProperty("outgoing_ports_min")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentOutgoingPortsMin { get; set; }

        /// <summary>
        /// The end of the range of ports to use for binding outgoing connections to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property together with <see cref="LibtorrentOutgoingPortsMin"/> defines the range of ports to use for binding outgoing connections to.
        /// The range shouldn't be too small.
        /// </para>
        /// <para>
        /// The value of <c>0</c> disables this setting.
        /// </para>
        /// <para>
        /// Setting outgoing ports will limit the ability to keep multiple connections to the same client, even for different torrents.
        /// It is not recommended to change this setting.
        /// Its main purpose is to use it as an escape hatch for cheap routers with QoS capability but can only classify flows based on port numbers.
        /// It is a range instead of a single port because of the problems with failing to reconnect to peers
        /// if a previous socket to that peer and port is in the waiting state.
        /// </para>
        /// </remarks>
        /// <seealso cref="LibtorrentOutgoingPortsMin"/>
        [JsonProperty("outgoing_ports_max")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentOutgoingPortsMax { get; set; }

        /// <summary>
        /// Determines how to treat TCP connections when there are uTP connections.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Since uTP is designed to yield to TCP, there's an inherent problem when using swarms that have both TCP and uTP connections.
        /// If nothing is done, uTP connections would often be starved out for bandwidth by the TCP connections.
        /// This mode is called <see cref="Client.UtpTcpMixedModeAlgorithm.PreferTcp"/> in qBittorrent.
        /// </para>
        /// The <see cref="Client.UtpTcpMixedModeAlgorithm.PeerProportional"/> mode on the other hand,
        /// simply looks at the current throughput
        /// and rate limits all TCP connections to their proportional share based on how many of the connections are TCP.
        /// This works best if uTP connections are not rate limited by the global rate limiter.
        /// </remarks>
        [JsonProperty("utp_tcp_mixed_mode")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public UtpTcpMixedModeAlgorithm? LibtorrentUtpTcpMixedModeAlgorithm { get; set; }

        /// <summary>
        /// Determines if connections from the same IP address as existing connections should be rejected or not.
        /// </summary>
        /// <remarks>
        /// This option is disabled by default to prevent abusive behavior by peers.
        /// It may be useful to allow such connections in cases where simulations are run on the same machine,
        /// and all peers in a swarm has the same IP address.
        /// </remarks>
        [JsonProperty("enable_multi_connections_from_same_ip")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? LibtorrentAllowMultipleConnectionsFromSameIp { get; set; }

        /// <summary>
        /// Enable qbittorrent's tracker functionality.
        /// </summary>
        /// <remarks>
        /// It is not a fully-featured bittorrent tracker, but it supports the basic features need for sharing a few torrents.
        /// </remarks>
        /// <seealso cref="LibtorrentEmbeddedTrackerPort"/>
        [JsonProperty("enable_embedded_tracker")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? LibtorrentEnableEmbeddedTracker { get; set; }

        /// <summary>
        /// The port the embedded tracker should listen on.
        /// </summary>
        /// <seealso cref="LibtorrentEnableEmbeddedTracker"/>
        [JsonProperty("embedded_tracker_port")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public int? LibtorrentEmbeddedTrackerPort { get; set; }

        /// <summary>
        /// Specifies which algorithm to use to determine which peers to unchoke.
        /// </summary>
        [JsonProperty("upload_slots_behavior")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public ChokingAlgorithm? LibtorrentUploadSlotsBehavior { get; set; }

        /// <summary>
        /// Controls the seeding unchoke behavior.
        /// </summary>
        [JsonProperty("upload_choking_algorithm")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public SeedChokingAlgorithm? LibtorrentUploadChokingAlgorithm { get; set; }

        /// <summary>
        /// Activate libtorrent's strict mode for super seeding.
        /// </summary>
        /// <remarks>
        /// When this is set to <see langword="true"/>, a piece has to have been forwarded to a third peer before another one is handed out.
        /// This is the traditional definition of super seeding.
        /// </remarks>
        [JsonProperty("enable_super_seeding")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        [Deprecated("2.5")]
        public bool? LibtorrentStrictSuperSeeding { get; set; }

        /// <summary>
        /// Controls how multi tracker torrents are treated.
        /// </summary>
        /// <remarks>
        /// If this is set to <see langword="true"/>, all trackers in the same tier are announced to in parallel.
        /// If all trackers in tier 0 fails, all trackers in tier 1 are announced as well.
        /// Otherwise the behavior is as defined by the multi tracker specification.
        /// </remarks>
        /// <seealso cref="LibtorrentAnnounceToAllTiers"/>
        [JsonProperty("announce_to_all_trackers")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? LibtorrentAnnounceToAllTrackers { get; set; }

        /// <summary>
        /// Controls how multi tracker torrents are treated.
        /// </summary>
        /// <remarks>
        /// When this is set to <see langword="true"/>, one tracker from each tier is announced to.
        /// This is the uTorrent behavior. This is false by default in order to comply with the multi-tracker specification.
        /// </remarks>
        /// <seealso cref="LibtorrentAnnounceToAllTrackers"/>
        [JsonProperty("announce_to_all_tiers")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public bool? LibtorrentAnnounceToAllTiers { get; set; }

        /// <summary>
        /// The ip address passed along to trackers. If this value is blank, the parameter will be omitted.
        /// </summary>
        [JsonProperty("announce_ip")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3")]
        public string LibtorrentAnnounceIp { get; set; }

        /// <summary>
        /// The number of seconds to wait when sending a stopped message before considering a tracker to have timed out.
        /// </summary>
        /// <remarks>
        /// This is usually shorter, to make the client quit faster. If the value is set to 0, the connections to trackers with the stopped event are suppressed.
        /// </remarks>
        [JsonProperty("stop_tracker_timeout")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.1")]
        public int? LibtorrentStopTrackerTimeout { get; set; }

        /// <summary>
        /// Limits the number of concurrent HTTP tracker announces. Once the limit is hit, tracker requests are queued and issued when an outstanding announce completes.
        /// </summary>
        [JsonProperty("max_concurrent_http_announces")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.6")]
        public int? LibtorrentMaxConcurrentHttpAnnounces { get; set; }

        /// <summary>
        /// The default torrent content layout.
        /// </summary>
        [JsonProperty("torrent_content_layout")]
        [JsonConverter(typeof(StringEnumConverter))]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.7")]
        public TorrentContentLayout? TorrentContentLayout { get; set; }

        /* Other */

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        [OnSerializing]
        internal void OnSerializingMethod(StreamingContext context)
        {
            if (WebUIPassword != null)
            {
                AdditionalData ??= new Dictionary<string, JToken>();
                AdditionalData[WebUiPasswordPropertyName] = WebUIPassword;
            }

            if (AddTorrentPaused != null)
            {
                AdditionalData ??= new Dictionary<string, JToken>();
                AdditionalData[AddTorrentStoppedPropertyName] = AddTorrentPaused;
            }
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (AdditionalData == null)
                return;

            if (AdditionalData.TryGetValue(WebUiPasswordPropertyName, out var hashToken)
                && hashToken.Type == JTokenType.String)
            {
                WebUIPasswordHash = hashToken.Value<string>();
                AdditionalData.Remove(WebUiPasswordPropertyName);
            }

            if (AdditionalData.TryGetValue(AddTorrentStoppedPropertyName, out var addTorrentStopped)
                && addTorrentStopped.Type == JTokenType.Boolean)
            {
                AddTorrentPaused = addTorrentStopped.Value<bool>();
                AdditionalData.Remove(AddTorrentStoppedPropertyName);
            }
        }
    }

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
        /// <param name="type">The type.</param>
        /// <returns>True if <paramref name="type"/> is nullable <see cref="ProxyType"/>.</returns>
        public override bool CanConvert(Type type) => type == typeof(ProxyType?);

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
            writer.WriteValue(_apiVersion < new ApiVersion(2, 9) ? (int?)proxyType : proxyType?.ToString().ToUpper());
        }
    }
}
