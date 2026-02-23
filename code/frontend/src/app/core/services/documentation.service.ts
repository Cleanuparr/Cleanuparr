import { inject, Injectable } from '@angular/core';
import { ApplicationPathService } from './base-path.service';

interface FieldMappings {
  [section: string]: { [field: string]: string };
}

@Injectable({ providedIn: 'root' })
export class DocumentationService {
  private readonly pathService = inject(ApplicationPathService);

  private readonly fieldMappings: FieldMappings = {
    'queue-cleaner': {
      'enabled': 'enable-queue-cleaner',
      'ignoredDownloads': 'ignored-downloads',
      'useAdvancedScheduling': 'scheduling-mode',
      'cronExpression': 'cron-expression',
      'failedImport.maxStrikes': 'failed-import-max-strikes',
      'failedImport.ignorePrivate': 'failed-import-ignore-private',
      'failedImport.deletePrivate': 'failed-import-delete-private',
      'failedImport.skipIfNotFoundInClient': 'failed-import-skip-if-not-found-in-client',
      'failedImport.pattern-mode': 'failed-import-pattern-mode',
      'failedImport.patterns': 'failed-import-patterns',
      'downloadingMetadataMaxStrikes': 'stalled-downloading-metadata-max-strikes',
      'stallRule.name': 'stalled-rule-name',
      'stallRule.enabled': 'stalled-enabled',
      'stallRule.maxStrikes': 'stalled-max-strikes',
      'stallRule.privacyType': 'stalled-privacy-type',
      'stallRule.completionRange': 'stalled-completion-percentage-range',
      'stallRule.resetStrikesOnProgress': 'stalled-reset-strikes-on-progress',
      'stallRule.minimumProgress': 'stalled-minimum-progress-to-reset',
      'stallRule.deletePrivateTorrentsFromClient': 'stalled-delete-private-from-client',
      'slowRule.name': 'slow-rule-name',
      'slowRule.enabled': 'slow-enabled',
      'slowRule.maxStrikes': 'slow-max-strikes',
      'slowRule.minSpeed': 'slow-min-speed',
      'slowRule.maxTimeHours': 'slow-maximum-time-hours',
      'slowRule.privacyType': 'slow-privacy-type',
      'slowRule.completionRange': 'slow-completion-percentage-range',
      'slowRule.ignoreAboveSize': 'slow-ignore-above-size',
      'slowRule.resetStrikesOnProgress': 'slow-reset-strikes-on-progress',
      'slowRule.deletePrivateTorrentsFromClient': 'slow-delete-private-from-client',
    },
    'general': {
      'displaySupportBanner': 'display-support-banner',
      'dryRun': 'dry-run',
      'httpMaxRetries': 'http-max-retries',
      'httpTimeout': 'http-timeout',
      'httpCertificateValidation': 'http-certificate-validation',
      'searchEnabled': 'search-enabled',
      'searchDelay': 'search-delay',
      'statusCheckEnabled': 'status-check',
      'log.level': 'log-level',
      'log.rollingSizeMB': 'rolling-size-mb',
      'log.retainedFileCount': 'retained-file-count',
      'log.timeLimitHours': 'time-limit',
      'log.archiveEnabled': 'archive-enabled',
      'log.archiveRetainedCount': 'archive-retained-count',
      'log.archiveTimeLimitHours': 'archive-time-limit',
      'ignoredDownloads': 'ignored-downloads',
      'strikeInactivityWindowHours': 'strike-inactivity-window',
      'auth.disableLocalAuth': 'disable-auth-for-local-addresses',
      'auth.trustForwardedHeaders': 'trust-forwarded-headers',
      'auth.trustedNetworks': 'additional-trusted-networks',
    },
    'download-cleaner': {
      'enabled': 'enable-download-cleaner',
      'ignoredDownloads': 'ignored-downloads',
      'useAdvancedScheduling': 'scheduling-mode',
      'cronExpression': 'cron-expression',
      'name': 'category-name',
      'privacyType': 'privacy-type',
      'maxRatio': 'max-ratio',
      'minSeedTime': 'min-seed-time',
      'maxSeedTime': 'max-seed-time',
      'deleteSourceFiles': 'delete-source-files',
      'unlinkedEnabled': 'enable-unlinked-download-handling',
      'unlinkedTargetCategory': 'target-category',
      'unlinkedUseTag': 'use-tag',
      'unlinkedIgnoredRootDir': 'ignored-root-directory',
      'unlinkedCategories': 'unlinked-categories',
    },
    'malware-blocker': {
      'enabled': 'enable-malware-blocker',
      'ignoredDownloads': 'ignored-downloads',
      'useAdvancedScheduling': 'scheduling-mode',
      'cronExpression': 'cron-expression',
      'ignorePrivate': 'ignore-private',
      'deletePrivate': 'delete-private',
      'deleteKnownMalware': 'delete-known-malware',
      'sonarr.enabled': 'enable-blocklist',
      'sonarr.blocklistPath': 'blocklist-path',
      'sonarr.blocklistType': 'blocklist-type',
      'radarr.enabled': 'enable-blocklist',
      'radarr.blocklistPath': 'blocklist-path',
      'radarr.blocklistType': 'blocklist-type',
      'lidarr.enabled': 'enable-blocklist',
      'lidarr.blocklistPath': 'blocklist-path',
      'lidarr.blocklistType': 'blocklist-type',
    },
    'download-client': {
      'enabled': 'enable-download-client',
      'name': 'client-name',
      'typeName': 'client-type',
      'host': 'client-host',
      'urlBase': 'url-base-path',
      'externalUrl': 'external-url',
      'username': 'username',
      'password': 'password',
    },
    'blacklist-sync': {
      'enabled': 'enable-blacklist-sync',
      'blacklistPath': 'blacklist-path',
    },
    'notifications': {
      'enabled': 'enabled',
      'name': 'provider-name',
      'eventTriggers': 'event-configuration',
      'onFailedImportStrike': 'event-configuration',
      'onStalledStrike': 'event-configuration',
      'onSlowStrike': 'event-configuration',
      'onQueueItemDeleted': 'event-configuration',
      'onDownloadCleaned': 'event-configuration',
      'onCategoryChanged': 'event-configuration',
    },
    'notifications/notifiarr': {
      'apiKey': 'api-key',
      'channelId': 'channel-id',
    },
    'notifications/apprise': {
      'mode': 'mode',
      'url': 'url',
      'key': 'key',
      'tags': 'tags',
      'serviceUrls': 'service-urls',
    },
    'notifications/ntfy': {
      'serverUrl': 'server-url',
      'topics': 'topics',
      'authenticationType': 'authentication-type',
      'username': 'username',
      'password': 'password',
      'accessToken': 'access-token',
      'priority': 'priority',
      'tags': 'tags',
    },
    'notifications/pushover': {
      'apiToken': 'api-token',
      'userKey': 'user-key',
      'devices': 'devices',
      'priority': 'priority',
      'retry': 'retry',
      'expire': 'expire',
      'sound': 'sound',
      'customSound': 'sound',
      'tags': 'tags',
    },
    'notifications/telegram': {
      'botToken': 'bot-token',
      'chatId': 'chat-id',
      'topicId': 'topic-id',
      'sendSilently': 'send-silently',
    },
    'notifications/discord': {
      'webhookUrl': 'webhook-url',
      'username': 'username',
      'avatarUrl': 'avatar-url',
    },
    'arr': {
      'enabled': 'enabled',
      'name': 'instance-name',
      'url': 'url',
      'externalUrl': 'external-url',
      'apiKey': 'api-key',
      'version': 'api-version',
    },
    'notifications/gotify': {
      'serverUrl': 'server-url',
      'applicationToken': 'application-token',
      'priority': 'priority',
    },
  };

  openFieldDocumentation(section: string, fieldName: string): void {
    const anchor = this.fieldMappings[section]?.[fieldName];
    const url = this.pathService.buildDocumentationUrl(section, anchor);
    window.open(url, '_blank', 'noopener,noreferrer');
  }

  getFieldDocumentationUrl(section: string, fieldName: string): string {
    const anchor = this.fieldMappings[section]?.[fieldName];
    return this.pathService.buildDocumentationUrl(section, anchor);
  }

  hasFieldDocumentation(section: string, fieldName: string): boolean {
    return !!this.fieldMappings[section]?.[fieldName];
  }
}
