import { Injectable } from '@angular/core';
import { ApplicationPathService } from './base-path.service';

export interface FieldDocumentationMapping {
  [section: string]: {
    [fieldName: string]: string; // anchor ID
  };
}

@Injectable({
  providedIn: 'root'
})
export class DocumentationService {
  
  // Field to anchor mappings for each configuration section
  private readonly fieldMappings: FieldDocumentationMapping = {
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
      'slowRule.deletePrivateTorrentsFromClient': 'slow-delete-private-from-client'
    },
    'general': {
      'displaySupportBanner': 'display-support-banner',
      'dryRun': 'dry-run',
      'httpMaxRetries': 'http-max-retries',
      'httpTimeout': 'http-timeout',
      'httpCertificateValidation': 'http-certificate-validation',
      'searchEnabled': 'search-enabled',
      'searchDelay': 'search-delay',
      'log.level': 'log-level',
      'log.rollingSizeMB': 'rolling-size-mb',
      'log.retainedFileCount': 'retained-file-count',
      'log.timeLimitHours': 'time-limit',
      'log.archiveEnabled': 'archive-enabled',
      'log.archiveRetainedCount': 'archive-retained-count',
      'log.archiveTimeLimitHours': 'archive-time-limit',
      'ignoredDownloads': 'ignored-downloads'
    },
    'download-cleaner': {
      'enabled': 'enable-download-cleaner',
      'ignoredDownloads': 'ignored-downloads',
      'useAdvancedScheduling': 'scheduling-mode',
      'cronExpression': 'cron-expression',
      'deletePrivate': 'delete-private-torrents',
      'name': 'category-name',
      'maxRatio': 'max-ratio',
      'minSeedTime': 'min-seed-time',
      'maxSeedTime': 'max-seed-time',
      'unlinkedEnabled': 'enable-unlinked-download-handling',
      'unlinkedTargetCategory': 'target-category',
      'unlinkedUseTag': 'use-tag',
      'unlinkedIgnoredRootDir': 'ignored-root-directory',
      'unlinkedCategories': 'unlinked-categories'
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
      'lidarr.blocklistType': 'blocklist-type'
    },
    'download-client': {
      'enabled': 'enable-download-client',
      'name': 'client-name',
      'typeName': 'client-type',
      'host': 'client-host',
      'urlBase': 'url-base-path',
      'username': 'username',
      'password': 'password'
    },
    'blacklist-sync': {
      'enabled': 'enable-blacklist-sync',
      'blacklistPath': 'blacklist-path'
    },
    'notifications': {
      'enabled': 'enabled',
      'name': 'provider-name',
      'eventTriggers': 'event-configuration'
    },
    'notifications/notifiarr': {
      'notifiarr.apiKey': 'api-key',
      'notifiarr.channelId': 'channel-id'
    },
    'notifications/apprise': {
      'apprise.url': 'url',
      'apprise.key': 'key',
      'apprise.tags': 'tags'
    },
    'notifications/ntfy': {
      'ntfy.serverUrl': 'server-url',
      'ntfy.topics': 'topics',
      'ntfy.authenticationType': 'authentication-type',
      'ntfy.username': 'username',
      'ntfy.password': 'password',
      'ntfy.accessToken': 'access-token',
      'ntfy.priority': 'priority',
      'ntfy.tags': 'tags'
    },
  };

  constructor(private applicationPathService: ApplicationPathService) {}

  /**
   * Opens documentation for a specific field in a new tab
   * @param section Configuration section (e.g., 'queue-cleaner')
   * @param fieldName Field name (e.g., 'enabled', 'failedImport.maxStrikes')
   */
  openFieldDocumentation(section: string, fieldName: string): void {
    const anchor = this.getFieldAnchor(section, fieldName);
    if (anchor) {
      const url = this.applicationPathService.buildDocumentationUrl(section, anchor);
      window.open(url, '_blank', 'noopener,noreferrer');
    } else {
      console.warn(`Documentation anchor not found for section: ${section}, field: ${fieldName}`);
      // Fallback: open section documentation without anchor
      const url = this.applicationPathService.buildDocumentationUrl(section);
      window.open(url, '_blank', 'noopener,noreferrer');
    }
  }

  /**
   * Gets the documentation URL for a specific field
   * @param section Configuration section
   * @param fieldName Field name
   * @returns Full documentation URL
   */
  getFieldDocumentationUrl(section: string, fieldName: string): string {
    const anchor = this.getFieldAnchor(section, fieldName);
    return this.applicationPathService.buildDocumentationUrl(section, anchor);
  }

  /**
   * Gets the anchor ID for a specific field
   * @param section Configuration section
   * @param fieldName Field name
   * @returns Anchor ID or undefined if not found
   */
  private getFieldAnchor(section: string, fieldName: string): string | undefined {
    return this.fieldMappings[section]?.[fieldName];
  }

  /**
   * Checks if documentation exists for a field
   * @param section Configuration section
   * @param fieldName Field name
   * @returns True if documentation exists
   */
  hasFieldDocumentation(section: string, fieldName: string): boolean {
    return !!this.getFieldAnchor(section, fieldName);
  }
}
