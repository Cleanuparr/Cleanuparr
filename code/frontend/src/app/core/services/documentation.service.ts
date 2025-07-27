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
      'useAdvancedScheduling': 'scheduling-mode',
      'cronExpression': 'cron-expression',
      'failedImport.maxStrikes': 'failed-import-max-strikes',
      'failedImport.ignorePrivate': 'failed-import-ignore-private',
      'failedImport.deletePrivate': 'failed-import-delete-private',
      'failedImport.ignoredPatterns': 'failed-import-ignored-patterns',
      'stalled.maxStrikes': 'stalled-max-strikes',
      'stalled.resetStrikesOnProgress': 'stalled-reset-strikes-on-progress',
      'stalled.ignorePrivate': 'stalled-ignore-private',
      'stalled.deletePrivate': 'stalled-delete-private',
      'stalled.downloadingMetadataMaxStrikes': 'downloading-metadata-max-strikes',
      'slow.maxStrikes': 'slow-max-strikes',
      'slow.resetStrikesOnProgress': 'slow-reset-strikes-on-progress',
      'slow.ignorePrivate': 'slow-ignore-private',
      'slow.deletePrivate': 'slow-delete-private',
      'slow.minSpeed': 'slow-min-speed',
      'slow.maxTime': 'slow-max-time',
      'slow.ignoreAboveSize': 'slow-ignore-above-size'
    },
    'general': {
      'displaySupportBanner': 'display-support-banner',
      'dryRun': 'dry-run',
      'httpMaxRetries': 'http-max-retries',
      'httpTimeout': 'http-timeout',
      'httpCertificateValidation': 'http-certificate-validation',
      'searchEnabled': 'search-enabled',
      'searchDelay': 'search-delay',
      'logLevel': 'log-level',
      'ignoredDownloads': 'ignored-downloads'
    },
    'download-cleaner': {
      'enabled': 'enable-download-cleaner',
      'useAdvancedScheduling': 'scheduling-mode',
      'cronExpression': 'cron-expression',
      'jobSchedule.every': 'run-schedule',
      'jobSchedule.type': 'run-schedule',
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
    'content-blocker': {
      'enabled': 'enable-content-blocker',
      'useAdvancedScheduling': 'scheduling-mode',
      'cronExpression': 'cron-expression',
      'jobSchedule.every': 'run-schedule',
      'jobSchedule.type': 'run-schedule',
      'ignorePrivate': 'ignore-private',
      'deletePrivate': 'delete-private',
      'sonarr.enabled': 'enable-sonarr-blocklist',
      'sonarr.blocklistPath': 'sonarr-blocklist-path',
      'sonarr.blocklistType': 'sonarr-blocklist-type',
      'radarr.enabled': 'enable-radarr-blocklist',
      'radarr.blocklistPath': 'radarr-blocklist-path',
      'radarr.blocklistType': 'radarr-blocklist-type',
      'lidarr.enabled': 'enable-lidarr-blocklist',
      'lidarr.blocklistPath': 'lidarr-blocklist-path',
      'lidarr.blocklistType': 'lidarr-blocklist-type'
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
    'notifications': {
      'notifiarr.apiKey': 'notifiarr-api-key',
      'notifiarr.channelId': 'notifiarr-channel-id',
      'apprise.fullUrl': 'apprise-url',
      'apprise.key': 'apprise-key',
      'eventTriggers': 'event-triggers'
    }
    // Additional sections will be added here as we implement them
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
