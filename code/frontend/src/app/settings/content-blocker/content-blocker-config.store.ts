import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { ContentBlockerConfig, JobSchedule, ScheduleUnit } from '../../shared/models/content-blocker-config.model';
import { ConfigurationService } from '../../core/services/configuration.service';
import { EMPTY, Observable, catchError, switchMap, tap, throwError } from 'rxjs';
import { ErrorHandlerUtil } from '../../core/utils/error-handler.util';

export interface ContentBlockerConfigState {
  config: ContentBlockerConfig | null;
  loading: boolean;
  saving: boolean;
  loadError: string | null;  // Only for load failures that should show "Not connected"
  saveError: string | null;  // Only for save failures that should show toast
}

const initialState: ContentBlockerConfigState = {
  config: null,
  loading: false,
  saving: false,
  loadError: null,
  saveError: null
};

@Injectable()
export class ContentBlockerConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    
    /**
     * Load the content blocker configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, loadError: null, saveError: null })),
        switchMap(() => configService.getContentBlockerConfig().pipe(
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
     * Save the content blocker configuration
     */
    saveConfig: rxMethod<ContentBlockerConfig>(
      (config$: Observable<ContentBlockerConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, saveError: null })),
        switchMap(config => configService.updateContentBlockerConfig(config).pipe(
          tap({
            next: () => {
              // Don't set config - let the form stay as-is with string enum values
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
     * Update config in the store without saving to the backend
     */
    updateConfigLocally(config: Partial<ContentBlockerConfig>) {
      const currentConfig = store.config();
      if (currentConfig) {
        patchState(store, {
          config: { ...currentConfig, ...config }
        });
      }
    },
    
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
    },

    /**
     * Generate a cron expression from a job schedule
     */
    generateCronExpression(schedule: JobSchedule): string {
      if (!schedule) {
        return "0/5 * * * * ?"; // Default: every 5 seconds (Quartz.NET format)
      }
      
      // Cron format: Seconds Minutes Hours Day-of-month Month Day-of-week Year
      switch (schedule.type) {
        case ScheduleUnit.Seconds:
          return `0/${schedule.every} * * ? * * *`; // Every n seconds (Quartz.NET format)
        
        case ScheduleUnit.Minutes:
          return `0 0/${schedule.every} * ? * * *`; // Every n minutes (Quartz.NET format)
        
        case ScheduleUnit.Hours:
          return `0 0 0/${schedule.every} ? * * *`; // Every n hours (Quartz.NET format)
        
        default:
          return "0/5 * * * * ?"; // Default: every 5 seconds (Quartz.NET format)
      }
    }
  })),
  withHooks({
    onInit({ loadConfig }) {
      loadConfig();
    }
  })
) {} 