import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { QueueCleanerConfig, JobSchedule, ScheduleUnit } from '../../shared/models/queue-cleaner-config.model';
import { 
  StallRule, 
  SlowRule, 
  CreateStallRuleDto,
  UpdateStallRuleDto,
  CreateSlowRuleDto,
  UpdateSlowRuleDto
} from '../../shared/models/queue-rule.model';
import { ConfigurationService } from '../../core/services/configuration.service';
import { EMPTY, Observable, catchError, switchMap, tap, forkJoin, of } from 'rxjs';
import { ErrorHandlerUtil } from '../../core/utils/error-handler.util';

export interface QueueCleanerConfigState {
  config: QueueCleanerConfig | null;
  stallRules: StallRule[];
  slowRules: SlowRule[];
  loading: boolean;
  saving: boolean;
  rulesLoading: boolean;
  rulesSaving: boolean;
  loadError: string | null;  // Only for load failures that should show "Not connected"
  saveError: string | null;  // Only for save failures that should show toast
  rulesError: string | null; // Errors related to rules operations
}

const initialState: QueueCleanerConfigState = {
  config: null,
  stallRules: [],
  slowRules: [],
  loading: false,
  saving: false,
  rulesLoading: false,
  rulesSaving: false,
  loadError: null,
  saveError: null,
  rulesError: null
};

@Injectable()
export class QueueCleanerConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    
    /**
     * Load the queue cleaner configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, loadError: null, saveError: null })),
        switchMap(() => configService.getQueueCleanerConfig().pipe(
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
     * Save the queue cleaner configuration
     */
    saveConfig: rxMethod<QueueCleanerConfig>(
      (config$: Observable<QueueCleanerConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, saveError: null })),
        switchMap(config => configService.updateQueueCleanerConfig(config).pipe(
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
    updateConfigLocally(config: Partial<QueueCleanerConfig>) {
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
        return "0 0/5 * * * ?"; // Default: every 5 minutes
      }
      
      // Cron format: Seconds Minutes Hours Day-of-month Month Day-of-week Year
      switch (schedule.type) {
        case ScheduleUnit.Seconds:
          return `0/${schedule.every} * * ? * * *`; // Every n seconds
        
        case ScheduleUnit.Minutes:
          return `0 0/${schedule.every} * ? * * *`; // Every n minutes
        
        case ScheduleUnit.Hours:
          return `0 0 0/${schedule.every} ? * * *`; // Every n hours
        
        default:
          return "0 0/5 * * * ?"; // Default: every 5 minutes
      }
    },

    // ===== QUEUE RULES METHODS =====

    /**
     * Load all queue rules (both stall and slow rules)
     */
    loadQueueRules: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { rulesLoading: true, rulesError: null })),
        switchMap(() => forkJoin({
          stallRules: configService.getStallRules().pipe(catchError(() => of([]))),
          slowRules: configService.getSlowRules().pipe(catchError(() => of([])))
        }).pipe(
          tap({
            next: ({ stallRules, slowRules }) => {
              patchState(store, { 
                stallRules, 
                slowRules, 
                rulesLoading: false, 
                rulesError: null 
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                rulesLoading: false, 
                rulesError: errorMessage
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              rulesLoading: false, 
              rulesError: errorMessage
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Create a new stall rule
     */
    createStallRule: rxMethod<CreateStallRuleDto>(
      (rule$: Observable<CreateStallRuleDto>) => rule$.pipe(
        tap(() => patchState(store, { rulesSaving: true, rulesError: null })),
        switchMap(rule => configService.createStallRule(rule).pipe(
          tap({
            next: (newRule) => {
              const currentRules = store.stallRules();
              patchState(store, { 
                stallRules: [...currentRules, newRule],
                rulesSaving: false,
                rulesError: null
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                rulesSaving: false, 
                rulesError: errorMessage
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              rulesSaving: false, 
              rulesError: errorMessage
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Update an existing stall rule
     */
    updateStallRule: rxMethod<{ id: string, rule: UpdateStallRuleDto }>(
      (data$: Observable<{ id: string, rule: UpdateStallRuleDto }>) => data$.pipe(
        tap(() => patchState(store, { rulesSaving: true, rulesError: null })),
        switchMap(({ id, rule }) => configService.updateStallRule(id, rule).pipe(
          tap({
            next: (updatedRule) => {
              const currentRules = store.stallRules();
              const updatedRules = currentRules.map(r => r.id === id ? updatedRule : r);
              patchState(store, { 
                stallRules: updatedRules,
                rulesSaving: false,
                rulesError: null
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                rulesSaving: false, 
                rulesError: errorMessage
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              rulesSaving: false, 
              rulesError: errorMessage
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Delete a stall rule
     */
    deleteStallRule: rxMethod<string>(
      (id$: Observable<string>) => id$.pipe(
        tap(() => patchState(store, { rulesSaving: true, rulesError: null })),
        switchMap(id => configService.deleteStallRule(id).pipe(
          tap({
            next: () => {
              const currentRules = store.stallRules();
              const filteredRules = currentRules.filter(r => r.id !== id);
              patchState(store, { 
                stallRules: filteredRules,
                rulesSaving: false,
                rulesError: null
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                rulesSaving: false, 
                rulesError: errorMessage
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              rulesSaving: false, 
              rulesError: errorMessage
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Create a new slow rule
     */
    createSlowRule: rxMethod<CreateSlowRuleDto>(
      (rule$: Observable<CreateSlowRuleDto>) => rule$.pipe(
        tap(() => patchState(store, { rulesSaving: true, rulesError: null })),
        switchMap(rule => configService.createSlowRule(rule).pipe(
          tap({
            next: (newRule) => {
              const currentRules = store.slowRules();
              patchState(store, { 
                slowRules: [...currentRules, newRule],
                rulesSaving: false,
                rulesError: null
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                rulesSaving: false, 
                rulesError: errorMessage
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              rulesSaving: false, 
              rulesError: errorMessage
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Update an existing slow rule
     */
    updateSlowRule: rxMethod<{ id: string, rule: UpdateSlowRuleDto }>(
      (data$: Observable<{ id: string, rule: UpdateSlowRuleDto }>) => data$.pipe(
        tap(() => patchState(store, { rulesSaving: true, rulesError: null })),
        switchMap(({ id, rule }) => configService.updateSlowRule(id, rule).pipe(
          tap({
            next: (updatedRule) => {
              const currentRules = store.slowRules();
              const updatedRules = currentRules.map(r => r.id === id ? updatedRule : r);
              patchState(store, { 
                slowRules: updatedRules,
                rulesSaving: false,
                rulesError: null
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                rulesSaving: false, 
                rulesError: errorMessage
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              rulesSaving: false, 
              rulesError: errorMessage
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Delete a slow rule
     */
    deleteSlowRule: rxMethod<string>(
      (id$: Observable<string>) => id$.pipe(
        tap(() => patchState(store, { rulesSaving: true, rulesError: null })),
        switchMap(id => configService.deleteSlowRule(id).pipe(
          tap({
            next: () => {
              const currentRules = store.slowRules();
              const filteredRules = currentRules.filter(r => r.id !== id);
              patchState(store, { 
                slowRules: filteredRules,
                rulesSaving: false,
                rulesError: null
              });
            },
            error: (error) => {
              const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
              patchState(store, { 
                rulesSaving: false, 
                rulesError: errorMessage
              });
            }
          }),
          catchError((error) => {
            const errorMessage = ErrorHandlerUtil.extractErrorMessage(error);
            patchState(store, { 
              rulesSaving: false, 
              rulesError: errorMessage
            });
            return EMPTY;
          })
        ))
      )
    ),

    /**
     * Clear rules errors
     */
    resetRulesError() {
      patchState(store, { rulesError: null });
    }
  })),
  withHooks({
    onInit({ loadConfig, loadQueueRules }) {
      loadConfig();
      loadQueueRules();
    }
  })
) {}
