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
  Seeker = 'Seeker',
}

export enum SelectionStrategy {
  BalancedWeighted = 'BalancedWeighted',
  OldestSearchFirst = 'OldestSearchFirst',
  OldestSearchWeighted = 'OldestSearchWeighted',
  NewestFirst = 'NewestFirst',
  NewestWeighted = 'NewestWeighted',
  Random = 'Random',
}

export enum SeriesSearchType {
  Season = 'Season',
  Series = 'Series',
}

export enum SearchCommandStatus {
  Pending = 'Pending',
  Started = 'Started',
  Completed = 'Completed',
  Failed = 'Failed',
  TimedOut = 'TimedOut',
}

export enum DeleteReason {
  None = 'None',
  Stalled = 'Stalled',
  FailedImport = 'FailedImport',
  DownloadingMetadata = 'DownloadingMetadata',
  SlowSpeed = 'SlowSpeed',
  SlowTime = 'SlowTime',
  AllFilesSkipped = 'AllFilesSkipped',
  AllFilesSkippedByQBit = 'AllFilesSkippedByQBit',
  AllFilesBlocked = 'AllFilesBlocked',
  AtLeastOneFileBlocked = 'AtLeastOneFileBlocked',
}

export enum CleanReason {
  None = 'None',
  MaxRatioReached = 'MaxRatioReached',
  MaxSeedTimeReached = 'MaxSeedTimeReached',
}

export type ArrType = 'sonarr' | 'radarr' | 'lidarr' | 'readarr' | 'whisparr';
