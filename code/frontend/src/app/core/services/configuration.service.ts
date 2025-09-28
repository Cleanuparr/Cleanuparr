import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, catchError, map, throwError } from "rxjs";
import { JobSchedule, QueueCleanerConfig, ScheduleUnit } from "../../shared/models/queue-cleaner-config.model";
import { MalwareBlockerConfig as MalwareBlockerConfig, JobSchedule as MalwareBlockerJobSchedule, ScheduleUnit as MalwareBlockerScheduleUnit } from "../../shared/models/malware-blocker-config.model";
import { SonarrConfig } from "../../shared/models/sonarr-config.model";
import { RadarrConfig } from "../../shared/models/radarr-config.model";
import { LidarrConfig } from "../../shared/models/lidarr-config.model";
import { ReadarrConfig } from "../../shared/models/readarr-config.model";
import { WhisparrConfig } from "../../shared/models/whisparr-config.model";
import { ClientConfig, DownloadClientConfig, CreateDownloadClientDto } from "../../shared/models/download-client-config.model";
import { ArrInstance, CreateArrInstanceDto } from "../../shared/models/arr-config.model";
import { GeneralConfig } from "../../shared/models/general-config.model";
import { 
  StallRule, 
  SlowRule, 
  CreateStallRuleDto,
  UpdateStallRuleDto,
  CreateSlowRuleDto,
  UpdateSlowRuleDto
} from "../../shared/models/queue-rule.model";
import { ApplicationPathService } from "./base-path.service";
import { ErrorHandlerUtil } from "../utils/error-handler.util";
import { BlacklistSyncConfig } from "../../shared/models/blacklist-sync-config.model";

@Injectable({
  providedIn: "root",
})
export class ConfigurationService {
  private readonly ApplicationPathService = inject(ApplicationPathService);
  private readonly http = inject(HttpClient);

  /**
   * Get general configuration
   */
  getGeneralConfig(): Observable<GeneralConfig> {
    return this.http.get<GeneralConfig>(this.ApplicationPathService.buildApiUrl('/configuration/general')).pipe(
      catchError((error) => {
        console.error("Error fetching general config:", error);
        return throwError(() => new Error("Failed to load general configuration"));
      })
    );
  }

  /**
   * Get Blacklist Sync configuration
   */
  getBlacklistSyncConfig(): Observable<BlacklistSyncConfig> {
    return this.http.get<BlacklistSyncConfig>(this.ApplicationPathService.buildApiUrl('/configuration/blacklist_sync')).pipe(
      catchError((error) => {
        console.error("Error fetching Blacklist Sync config:", error);
        return throwError(() => new Error("Failed to load Blacklist Sync configuration"));
      })
    );
  }

  /**
   * Update Blacklist Sync configuration
   */
  updateBlacklistSyncConfig(config: BlacklistSyncConfig): Observable<any> {
    return this.http.put<any>(this.ApplicationPathService.buildApiUrl('/configuration/blacklist_sync'), config).pipe(
      catchError((error) => {
        console.error("Error updating Blacklist Sync config:", error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Update general configuration
   */
  updateGeneralConfig(config: GeneralConfig): Observable<any> {
    return this.http.put<any>(this.ApplicationPathService.buildApiUrl('/configuration/general'), config).pipe(
      catchError((error) => {
        console.error("Error updating general config:", error);
        return throwError(() => new Error(error.error?.error || "Failed to update general configuration"));
      })
    );
  }

  /**
   * Get queue cleaner configuration
   */
  getQueueCleanerConfig(): Observable<QueueCleanerConfig> {
    return this.http.get<QueueCleanerConfig>(this.ApplicationPathService.buildApiUrl('/configuration/queue_cleaner')).pipe(
      map((response) => {
        response.jobSchedule = this.tryExtractJobScheduleFromCron(response.cronExpression);
        return response;
      }),
      catchError((error) => {
        console.error("Error fetching queue cleaner config:", error);
        return throwError(() => new Error("Failed to load queue cleaner configuration"));
      })
    );
  }

  /**
   * Update queue cleaner configuration
   */
  updateQueueCleanerConfig(config: QueueCleanerConfig): Observable<QueueCleanerConfig> {
    // Generate cron expression if using basic scheduling
    if (!config.useAdvancedScheduling && config.jobSchedule) {
      config.cronExpression = this.convertJobScheduleToCron(config.jobSchedule);
    }
    return this.http.put<QueueCleanerConfig>(this.ApplicationPathService.buildApiUrl('/configuration/queue_cleaner'), config).pipe(
      catchError((error) => {
        console.error("Error updating queue cleaner config:", error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Get content blocker configuration
   */
  getMalwareBlockerConfig(): Observable<MalwareBlockerConfig> {
    return this.http.get<MalwareBlockerConfig>(this.ApplicationPathService.buildApiUrl('/configuration/malware_blocker')).pipe(
      map((response) => {
        response.jobSchedule = this.tryExtractMalwareBlockerJobScheduleFromCron(response.cronExpression);
        return response;
      }),
      catchError((error) => {
        console.error("Error fetching Malware Blocker config:", error);
        return throwError(() => new Error("Failed to load Malware Blocker configuration"));
      })
    );
  }

  /**
   * Update content blocker configuration
   */
  updateMalwareBlockerConfig(config: MalwareBlockerConfig): Observable<void> {
    // Generate cron expression if using basic scheduling
    if (!config.useAdvancedScheduling && config.jobSchedule) {
      config.cronExpression = this.convertMalwareBlockerJobScheduleToCron(config.jobSchedule);
    }
    return this.http.put<void>(this.ApplicationPathService.buildApiUrl('/configuration/malware_blocker'), config).pipe(
      catchError((error) => {
        console.error("Error updating Malware Blocker config:", error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Try to extract a JobSchedule from a cron expression
   * Only handles the simple cases we're generating
   */
  private tryExtractJobScheduleFromCron(cronExpression: string): JobSchedule | undefined {
    // Patterns we support:
    // Seconds: */n * * ? * * * or 0/n * * ? * * * (Quartz.NET format)
    // Minutes: 0 */n * ? * * * or 0 0/n * ? * * * (Quartz.NET format)
    // Hours: 0 0 */n ? * * * or 0 0 0/n ? * * * (Quartz.NET format)
    try {
      const parts = cronExpression.split(" ");

      if (parts.length !== 7) return undefined;

      // Every n seconds - handle both */n and 0/n formats
      if ((parts[0].startsWith("*/") || parts[0].startsWith("0/")) && parts[1] === "*") {
        const seconds = parseInt(parts[0].substring(2));
        if (!isNaN(seconds) && seconds > 0 && seconds < 60) {
          return { every: seconds, type: ScheduleUnit.Seconds };
        }
      }

      // Every n minutes - handle both */n and 0/n formats
      if (parts[0] === "0" && (parts[1].startsWith("*/") || parts[1].startsWith("0/"))) {
        const minutes = parseInt(parts[1].substring(2));
        if (!isNaN(minutes) && minutes > 0 && minutes < 60) {
          return { every: minutes, type: ScheduleUnit.Minutes };
        }
      }

      // Every n hours - handle both */n and 0/n formats
      if (parts[0] === "0" && parts[1] === "0" && (parts[2].startsWith("*/") || parts[2].startsWith("0/"))) {
        const hours = parseInt(parts[2].substring(2));
        if (!isNaN(hours) && hours > 0 && hours < 24) {
          return { every: hours, type: ScheduleUnit.Hours };
        }
      }
    } catch (e) {
      console.warn("Could not parse cron expression:", cronExpression);
    }

    return undefined;
  }

  /**
   * Convert a JobSchedule to a cron expression
   */
  private convertJobScheduleToCron(schedule: JobSchedule): string {
    if (!schedule || schedule.every <= 0) {
      return "0 0/5 * * * ?"; // Default: every 5 minutes (Quartz.NET format)
    }

    switch (schedule.type) {
      case ScheduleUnit.Seconds:
        if (schedule.every < 60) {
          return `0/${schedule.every} * * ? * * *`; // Quartz.NET format
        }
        break;

      case ScheduleUnit.Minutes:
        if (schedule.every < 60) {
          return `0 0/${schedule.every} * ? * * *`; // Quartz.NET format
        }
        break;

      case ScheduleUnit.Hours:
        if (schedule.every < 24) {
          return `0 0 0/${schedule.every} ? * * *`; // Quartz.NET format
        }
        break;
    }

    // Fallback to default
    return "0 0/5 * * * ?"; // Default: every 5 minutes (Quartz.NET format)
  }

  /**
   * Try to extract a MalwareBlockerJobSchedule from a cron expression
   * Only handles the simple cases we're generating
   */
  private tryExtractMalwareBlockerJobScheduleFromCron(cronExpression: string): MalwareBlockerJobSchedule | undefined {
    // Patterns we support:
    // Seconds: */n * * ? * * * or 0/n * * ? * * * (Quartz.NET format)
    // Minutes: 0 */n * ? * * * or 0 0/n * ? * * * (Quartz.NET format)
    // Hours: 0 0 */n ? * * * or 0 0 0/n ? * * * (Quartz.NET format)
    try {
      const parts = cronExpression.split(" ");

      if (parts.length !== 7) return undefined;

      // Every n seconds - handle both */n and 0/n formats
      if ((parts[0].startsWith("*/") || parts[0].startsWith("0/")) && parts[1] === "*") {
        const seconds = parseInt(parts[0].substring(2));
        if (!isNaN(seconds) && seconds > 0 && seconds < 60) {
          return { every: seconds, type: MalwareBlockerScheduleUnit.Seconds };
        }
      }

      // Every n minutes - handle both */n and 0/n formats
      if (parts[0] === "0" && (parts[1].startsWith("*/") || parts[1].startsWith("0/"))) {
        const minutes = parseInt(parts[1].substring(2));
        if (!isNaN(minutes) && minutes > 0 && minutes < 60) {
          return { every: minutes, type: MalwareBlockerScheduleUnit.Minutes };
        }
      }

      // Every n hours - handle both */n and 0/n formats
      if (parts[0] === "0" && parts[1] === "0" && (parts[2].startsWith("*/") || parts[2].startsWith("0/"))) {
        const hours = parseInt(parts[2].substring(2));
        if (!isNaN(hours) && hours > 0 && hours < 24) {
          return { every: hours, type: MalwareBlockerScheduleUnit.Hours };
        }
      }
    } catch (e) {
      console.warn("Could not parse cron expression:", cronExpression);
    }

    return undefined;
  }

  /**
   * Convert a MalwareBlockerJobSchedule to a cron expression
   */
  private convertMalwareBlockerJobScheduleToCron(schedule: MalwareBlockerJobSchedule): string {
    if (!schedule || schedule.every <= 0) {
      return "0/5 * * * * ?"; // Default: every 5 seconds (Quartz.NET format)
    }

    switch (schedule.type) {
      case MalwareBlockerScheduleUnit.Seconds:
        if (schedule.every < 60) {
          return `0/${schedule.every} * * ? * * *`; // Quartz.NET format
        }
        break;

      case MalwareBlockerScheduleUnit.Minutes:
        if (schedule.every < 60) {
          return `0 0/${schedule.every} * ? * * *`; // Quartz.NET format
        }
        break;

      case MalwareBlockerScheduleUnit.Hours:
        if (schedule.every < 24) {
          return `0 0 0/${schedule.every} ? * * *`; // Quartz.NET format
        }
        break;
    }

    // Fallback to default
    return "0/5 * * * * ?"; // Default: every 5 seconds (Quartz.NET format)
  }

  /**
   * Get Sonarr configuration
   */
  getSonarrConfig(): Observable<SonarrConfig> {
    return this.http.get<SonarrConfig>(this.ApplicationPathService.buildApiUrl('/configuration/sonarr')).pipe(
      catchError((error) => {
        console.error("Error fetching Sonarr config:", error);
        return throwError(() => new Error("Failed to load Sonarr configuration"));
      })
    );
  }
  /**
   * Update Sonarr configuration (global settings only)
   */
  updateSonarrConfig(config: {failedImportMaxStrikes: number}): Observable<any> {
    return this.http.put<any>(this.ApplicationPathService.buildApiUrl('/configuration/sonarr'), config).pipe(
      catchError((error) => {
        console.error("Error updating Sonarr config:", error);
        return throwError(() => new Error(error.error?.error || "Failed to update Sonarr configuration"));
      })
    );
  }

  /**
   * Get Radarr configuration
   */
  getRadarrConfig(): Observable<RadarrConfig> {
    return this.http.get<RadarrConfig>(this.ApplicationPathService.buildApiUrl('/configuration/radarr')).pipe(
      catchError((error) => {
        console.error("Error fetching Radarr config:", error);
        return throwError(() => new Error("Failed to load Radarr configuration"));
      })
    );
  }
  /**
   * Update Radarr configuration
   */
  updateRadarrConfig(config: {failedImportMaxStrikes: number}): Observable<any> {
    return this.http.put<any>(this.ApplicationPathService.buildApiUrl('/configuration/radarr'), config).pipe(
      catchError((error) => {
        console.error("Error updating Radarr config:", error);
        return throwError(() => new Error(error.error?.error || "Failed to update Radarr configuration"));
      })
    );
  }

  /**
   * Get Lidarr configuration
   */
  getLidarrConfig(): Observable<LidarrConfig> {
    return this.http.get<LidarrConfig>(this.ApplicationPathService.buildApiUrl('/configuration/lidarr')).pipe(
      catchError((error) => {
        console.error("Error fetching Lidarr config:", error);
        return throwError(() => new Error("Failed to load Lidarr configuration"));
      })
    );
  }
  /**
   * Update Lidarr configuration
   */
  updateLidarrConfig(config: {failedImportMaxStrikes: number}): Observable<any> {
    return this.http.put<any>(this.ApplicationPathService.buildApiUrl('/configuration/lidarr'), config).pipe(
      catchError((error) => {
        console.error("Error updating Lidarr config:", error);
        return throwError(() => new Error(error.error?.error || "Failed to update Lidarr configuration"));
      })
    );
  }

  /**
   * Get Readarr configuration
   */
  getReadarrConfig(): Observable<ReadarrConfig> {
    return this.http.get<ReadarrConfig>(this.ApplicationPathService.buildApiUrl('/configuration/readarr')).pipe(
      catchError((error) => {
        console.error("Error fetching Readarr config:", error);
        return throwError(() => new Error("Failed to load Readarr configuration"));
      })
    );
  }
  /**
   * Update Readarr configuration
   */
  updateReadarrConfig(config: {failedImportMaxStrikes: number}): Observable<any> {
    return this.http.put<any>(this.ApplicationPathService.buildApiUrl('/configuration/readarr'), config).pipe(
      catchError((error) => {
        console.error("Error updating Readarr config:", error);
        return throwError(() => new Error(error.error?.error || "Failed to update Readarr configuration"));
      })
    );
  }

  /**
   * Get Whisparr configuration
   */
  getWhisparrConfig(): Observable<WhisparrConfig> {
    return this.http.get<WhisparrConfig>(this.ApplicationPathService.buildApiUrl('/configuration/whisparr')).pipe(
      catchError((error) => {
        console.error("Error fetching Whisparr config:", error);
        return throwError(() => new Error("Failed to load Whisparr configuration"));
      })
    );
  }
  /**
   * Update Whisparr configuration
   */
  updateWhisparrConfig(config: {failedImportMaxStrikes: number}): Observable<any> {
    return this.http.put<any>(this.ApplicationPathService.buildApiUrl('/configuration/whisparr'), config).pipe(
      catchError((error) => {
        console.error("Error updating Whisparr config:", error);
        return throwError(() => new Error(error.error?.error || "Failed to update Whisparr configuration"));
      })
    );
  }

  /**
   * Get Download Client configuration
   */
  getDownloadClientConfig(): Observable<DownloadClientConfig> {
    return this.http.get<DownloadClientConfig>(this.ApplicationPathService.buildApiUrl('/configuration/download_client')).pipe(
      catchError((error) => {
        console.error("Error fetching Download Client config:", error);
        return throwError(() => new Error("Failed to load Download Client configuration"));
      })
    );
  }
  
  /**
   * Update Download Client configuration
   */
  updateDownloadClientConfig(config: DownloadClientConfig): Observable<DownloadClientConfig> {
    return this.http.put<DownloadClientConfig>(this.ApplicationPathService.buildApiUrl('/configuration/download_client'), config).pipe(
      catchError((error) => {
        console.error("Error updating Download Client config:", error);
        return throwError(() => new Error(error.error?.error || "Failed to update Download Client configuration"));
      })
    );
  }
  
  /**
   * Create a new Download Client
   */
  createDownloadClient(client: CreateDownloadClientDto): Observable<ClientConfig> {
    return this.http.post<ClientConfig>(this.ApplicationPathService.buildApiUrl('/configuration/download_client'), client).pipe(
      catchError((error) => {
        console.error("Error creating Download Client:", error);
        return throwError(() => new Error(error.error?.error || "Failed to create Download Client"));
      })
    );
  }
  
  /**
   * Update a specific Download Client by ID
   */
  updateDownloadClient(id: string, client: ClientConfig): Observable<ClientConfig> {
    return this.http.put<ClientConfig>(this.ApplicationPathService.buildApiUrl(`/configuration/download_client/${id}`), client).pipe(
      catchError((error) => {
        console.error(`Error updating Download Client with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to update Download Client with ID ${id}`));
      })
    );
  }
  
  /**
   * Delete a Download Client by ID
   */
  deleteDownloadClient(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/configuration/download_client/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting Download Client with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to delete Download Client with ID ${id}`));
      })
    );
  }

  // ===== SONARR INSTANCE MANAGEMENT =====

  /**
   * Create a new Sonarr instance
   */
  createSonarrInstance(instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.post<ArrInstance>(this.ApplicationPathService.buildApiUrl('/configuration/sonarr/instances'), instance).pipe(
      catchError((error) => {
        console.error("Error creating Sonarr instance:", error);
        return throwError(() => new Error(error.error?.error || "Failed to create Sonarr instance"));
      })
    );
  }

  /**
   * Update a Sonarr instance by ID
   */
  updateSonarrInstance(id: string, instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.put<ArrInstance>(this.ApplicationPathService.buildApiUrl(`/configuration/sonarr/instances/${id}`), instance).pipe(
      catchError((error) => {
        console.error(`Error updating Sonarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to update Sonarr instance with ID ${id}`));
      })
    );
  }

  /**
   * Delete a Sonarr instance by ID
   */
  deleteSonarrInstance(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/configuration/sonarr/instances/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting Sonarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to delete Sonarr instance with ID ${id}`));
      })
    );
  }

  // ===== RADARR INSTANCE MANAGEMENT =====

  /**
   * Create a new Radarr instance
   */
  createRadarrInstance(instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.post<ArrInstance>(this.ApplicationPathService.buildApiUrl('/configuration/radarr/instances'), instance).pipe(
      catchError((error) => {
        console.error("Error creating Radarr instance:", error);
        return throwError(() => new Error(error.error?.error || "Failed to create Radarr instance"));
      })
    );
  }

  /**
   * Update a Radarr instance by ID
   */
  updateRadarrInstance(id: string, instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.put<ArrInstance>(this.ApplicationPathService.buildApiUrl(`/configuration/radarr/instances/${id}`), instance).pipe(
      catchError((error) => {
        console.error(`Error updating Radarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to update Radarr instance with ID ${id}`));
      })
    );
  }

  /**
   * Delete a Radarr instance by ID
   */
  deleteRadarrInstance(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/configuration/radarr/instances/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting Radarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to delete Radarr instance with ID ${id}`));
      })
    );
  }

  // ===== LIDARR INSTANCE MANAGEMENT =====

  /**
   * Create a new Lidarr instance
   */
  createLidarrInstance(instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.post<ArrInstance>(this.ApplicationPathService.buildApiUrl('/configuration/lidarr/instances'), instance).pipe(
      catchError((error) => {
        console.error("Error creating Lidarr instance:", error);
        return throwError(() => new Error(error.error?.error || "Failed to create Lidarr instance"));
      })
    );
  }

  /**
   * Update a Lidarr instance by ID
   */
  updateLidarrInstance(id: string, instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.put<ArrInstance>(this.ApplicationPathService.buildApiUrl(`/configuration/lidarr/instances/${id}`), instance).pipe(
      catchError((error) => {
        console.error(`Error updating Lidarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to update Lidarr instance with ID ${id}`));
      })
    );
  }

  /**
   * Delete a Lidarr instance by ID
   */
  deleteLidarrInstance(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/configuration/lidarr/instances/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting Lidarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to delete Lidarr instance with ID ${id}`));
      })
    );
  }

  // ===== READARR INSTANCE MANAGEMENT =====

  /**
   * Create a new Readarr instance
   */
  createReadarrInstance(instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.post<ArrInstance>(this.ApplicationPathService.buildApiUrl('/configuration/readarr/instances'), instance).pipe(
      catchError((error) => {
        console.error("Error creating Readarr instance:", error);
        return throwError(() => new Error(error.error?.error || "Failed to create Readarr instance"));
      })
    );
  }

  /**
   * Update a Readarr instance by ID
   */
  updateReadarrInstance(id: string, instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.put<ArrInstance>(this.ApplicationPathService.buildApiUrl(`/configuration/readarr/instances/${id}`), instance).pipe(
      catchError((error) => {
        console.error(`Error updating Readarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to update Readarr instance with ID ${id}`));
      })
    );
  }

  /**
   * Delete a Readarr instance by ID
   */
  deleteReadarrInstance(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/configuration/readarr/instances/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting Readarr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to delete Readarr instance with ID ${id}`));
      })
    );
  }

  // ===== WHISPARR INSTANCE MANAGEMENT =====

  /**
   * Create a new Whisparr instance
   */
  createWhisparrInstance(instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.post<ArrInstance>(this.ApplicationPathService.buildApiUrl('/configuration/whisparr/instances'), instance).pipe(
      catchError((error) => {
        console.error("Error creating Whisparr instance:", error);
        return throwError(() => new Error(error.error?.error || "Failed to create Whisparr instance"));
      })
    );
  }

  /**
   * Update a Whisparr instance by ID
   */
  updateWhisparrInstance(id: string, instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.put<ArrInstance>(this.ApplicationPathService.buildApiUrl(`/configuration/whisparr/instances/${id}`), instance).pipe(
      catchError((error) => {
        console.error(`Error updating Whisparr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to update Whisparr instance with ID ${id}`));
      })
    );
  }

  /**
   * Delete a Whisparr instance by ID
   */
  deleteWhisparrInstance(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/configuration/whisparr/instances/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting Whisparr instance with ID ${id}:`, error);
        return throwError(() => new Error(error.error?.error || `Failed to delete Whisparr instance with ID ${id}`));
      })
    );
  }

  // ===== QUEUE RULES MANAGEMENT =====

  /**
   * Get all stall rules
   */
  getStallRules(): Observable<StallRule[]> {
    return this.http.get<StallRule[]>(this.ApplicationPathService.buildApiUrl('/queue-rules/stall')).pipe(
      catchError((error) => {
        console.error('Error fetching stall rules:', error);
        return throwError(() => new Error("Failed to load stall rules"));
      })
    );
  }

  /**
   * Create a new stall rule
   */
  createStallRule(rule: CreateStallRuleDto): Observable<StallRule> {
    return this.http.post<StallRule>(this.ApplicationPathService.buildApiUrl('/queue-rules/stall'), rule).pipe(
      catchError((error) => {
        console.error('Error creating stall rule:', error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Update an existing stall rule
   */
  updateStallRule(id: string, rule: UpdateStallRuleDto): Observable<StallRule> {
    return this.http.put<StallRule>(this.ApplicationPathService.buildApiUrl(`/queue-rules/stall/${id}`), rule).pipe(
      catchError((error) => {
        console.error(`Error updating stall rule ${id}:`, error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Delete a stall rule
   */
  deleteStallRule(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/queue-rules/stall/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting stall rule ${id}:`, error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Get all slow rules
   */
  getSlowRules(): Observable<SlowRule[]> {
    return this.http.get<SlowRule[]>(this.ApplicationPathService.buildApiUrl('/queue-rules/slow')).pipe(
      catchError((error) => {
        console.error('Error fetching slow rules:', error);
        return throwError(() => new Error("Failed to load slow rules"));
      })
    );
  }

  /**
   * Create a new slow rule
   */
  createSlowRule(rule: CreateSlowRuleDto): Observable<SlowRule> {
    return this.http.post<SlowRule>(this.ApplicationPathService.buildApiUrl('/queue-rules/slow'), rule).pipe(
      catchError((error) => {
        console.error('Error creating slow rule:', error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Update an existing slow rule
   */
  updateSlowRule(id: string, rule: UpdateSlowRuleDto): Observable<SlowRule> {
    return this.http.put<SlowRule>(this.ApplicationPathService.buildApiUrl(`/queue-rules/slow/${id}`), rule).pipe(
      catchError((error) => {
        console.error(`Error updating slow rule ${id}:`, error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }

  /**
   * Delete a slow rule
   */
  deleteSlowRule(id: string): Observable<void> {
    return this.http.delete<void>(this.ApplicationPathService.buildApiUrl(`/queue-rules/slow/${id}`)).pipe(
      catchError((error) => {
        console.error(`Error deleting slow rule ${id}:`, error);
        const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
        return throwError(() => new Error(errorMessage));
      })
    );
  }
}
