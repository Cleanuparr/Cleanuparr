export enum DownloadClientType {
  Torrent = 'Torrent',
  Usenet = 'Usenet',
}

export enum DownloadClientTypeName {
  qBittorrent = 'qBittorrent',
  Deluge = 'Deluge',
  Transmission = 'Transmission',
  uTorrent = 'uTorrent',
  rTorrent = 'rTorrent',
}

export enum NotificationProviderType {
  Notifiarr = 'Notifiarr',
  Apprise = 'Apprise',
  Ntfy = 'Ntfy',
  Pushover = 'Pushover',
  Telegram = 'Telegram',
  Discord = 'Discord',
  Gotify = 'Gotify',
}

export enum AppriseMode {
  Api = 'Api',
  Cli = 'Cli',
}

export enum CertificateValidationType {
  Enabled = 'Enabled',
  DisabledForLocalAddresses = 'DisabledForLocalAddresses',
  Disabled = 'Disabled',
}

export enum LogEventLevel {
  Verbose = 'Verbose',
  Debug = 'Debug',
  Information = 'Information',
  Warning = 'Warning',
  Error = 'Error',
  Fatal = 'Fatal',
}

export enum ScheduleUnit {
  Seconds = 'Seconds',
  Minutes = 'Minutes',
  Hours = 'Hours',
}

export enum PatternMode {
  Exclude = 'Exclude',
  Include = 'Include',
}

export enum BlocklistType {
  Blacklist = 'Blacklist',
  Whitelist = 'Whitelist',
}

export enum TorrentPrivacyType {
  Public = 'Public',
  Private = 'Private',
  Both = 'Both',
}

export enum NtfyAuthenticationType {
  None = 'None',
  BasicAuth = 'BasicAuth',
  AccessToken = 'AccessToken',
}

export enum NtfyPriority {
  Min = 'Min',
  Low = 'Low',
  Default = 'Default',
  High = 'High',
  Max = 'Max',
}

export enum PushoverPriority {
  Lowest = 'Lowest',
  Low = 'Low',
  Normal = 'Normal',
  High = 'High',
  Emergency = 'Emergency',
}

export enum JobType {
  QueueCleaner = 'QueueCleaner',
  MalwareBlocker = 'MalwareBlocker',
  DownloadCleaner = 'DownloadCleaner',
  BlacklistSynchronizer = 'BlacklistSynchronizer',
}

export type ArrType = 'sonarr' | 'radarr' | 'lidarr' | 'readarr' | 'whisparr';
