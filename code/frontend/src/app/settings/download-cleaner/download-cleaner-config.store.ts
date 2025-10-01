import { Injectable, inject } from "@angular/core";
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { HttpClient, HttpErrorResponse } from "@angular/common/http";
import { EMPTY, Observable, catchError, switchMap, tap } from 'rxjs';
import { DownloadCleanerConfig } from "../../shared/models/download-cleaner-config.model";
import { ApplicationPathService } from "../../core/services/base-path.service";
import { ErrorHandlerUtil } from "../../core/utils/error-handler.util";

export interface DownloadCleanerConfigState {
  config: DownloadCleanerConfig | null;
  loading: boolean;
  saving: boolean;
  loadError: string | null;  // Only for load failures that should show "Not connected"
  saveError: string | null;  // Only for save failures that should show toast
}

const initialState: DownloadCleanerConfigState = {
  config: null,
  loading: false,
  saving: false,
  loadError: null,
  saveError: null
};

export const DownloadCleanerConfigStore = signalStore(
  withState(initialState),
  withMethods((store, http = inject(HttpClient), applicationPathService = inject(ApplicationPathService)) => ({
    
    /**
     * Load download cleaner configuration from the API
     */
    loadDownloadCleanerConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, loadError: null, saveError: null })),
        switchMap(() => http.get<DownloadCleanerConfig>(applicationPathService.buildApiUrl('/configuration/download_cleaner')).pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false, loadError: null }),
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                loading: false, 
                loadError: errorMessage  // Only load errors should trigger "Not connected" state
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              loading: false, 
              loadError: errorMessage  // Only load errors should trigger "Not connected" state
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Generate a cron expression from a job schedule
     */
    generateCronExpression(schedule: { every: number; type: string }): string {
      if (!schedule) {
        return "0 0 * * * ?"; // Default: every hour
      }
      
      // Cron format: Seconds Minutes Hours Day-of-month Month Day-of-week Year
      switch (schedule.type) {
        case 'Seconds':
          return `0/${schedule.every} * * ? * * *`; // Every n seconds
        
        case 'Minutes':
          return `0 0/${schedule.every} * ? * * *`; // Every n minutes
        
        case 'Hours':
          return `0 0 0/${schedule.every} ? * * *`; // Every n hours
        
        default:
          return "0 0 * * * ?"; // Default: every hour
      }
    },

    /**
     * Parse a cron expression back to a job schedule
     */
    parseCronExpression(cronExpression: string): { every: number; type: string } | null {
      if (!cronExpression) {
        return null;
      }

      // Handle common patterns
      const patterns = [
        // Every n seconds: "0/n * * ? * * *"
        { regex: /^0\/(\d+) \* \* \? \* \* \*$/, type: 'Seconds' },
        // Every n minutes: "0 0/n * ? * * *"
        { regex: /^0 0\/(\d+) \* \? \* \* \*$/, type: 'Minutes' },
        // Every n hours: "0 0 0/n ? * * *"
        { regex: /^0 0 0\/(\d+) \? \* \* \*$/, type: 'Hours' },
      ];

      for (const pattern of patterns) {
        const match = cronExpression.match(pattern.regex);
        if (match) {
          const every = parseInt(match[1], 10);
          return { every, type: pattern.type };
        }
      }

      return null; // Couldn't parse, use advanced mode
    },

    /**
     * Save download cleaner configuration to the API
     */
    saveDownloadCleanerConfig: rxMethod<DownloadCleanerConfig>(
      (config$: Observable<DownloadCleanerConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, saveError: null })),
        switchMap(config => http.put<any>(applicationPathService.buildApiUrl('/configuration/download_cleaner'), config).pipe(
          tap({
            next: () => {
              // Successfully saved - just update saving state
              // Don't update config to avoid triggering form effects
              patchState(store, { 
                saving: false,
                saveError: null  // Clear any previous save errors
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                saving: false, 
                saveError: errorMessage  // Save errors should NOT trigger "Not connected" state
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              saving: false, 
              saveError: errorMessage  // Save errors should NOT trigger "Not connected" state
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Reset any errors
     */
    resetError() {
      patchState(store, { loadError: null, saveError: null });
    },
    
    /**
     * Reset only save errors (for when user fixes validation issues)
     */
    resetSaveError() {
      patchState(store, { saveError: null });
    }
  })),
  withHooks({
    onInit({ loadDownloadCleanerConfig }) {
      loadDownloadCleanerConfig();
    }
  })
);

export type DownloadCleanerConfigStore = InstanceType<typeof DownloadCleanerConfigStore>;
