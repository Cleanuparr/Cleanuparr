import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { GeneralConfig } from '../../shared/models/general-config.model';
import { LoggingConfig } from '../../shared/models/logging-config.model';
import { ConfigurationService } from '../../core/services/configuration.service';
import { EMPTY, Observable, catchError, switchMap, tap } from 'rxjs';

export interface GeneralConfigState {
  config: GeneralConfig | null;
  loading: boolean;
  saving: boolean;
  loadError: string | null;  // Only for load failures that should show "Not connected"
  saveError: string | null;  // Only for save failures that should show toast
}

const initialState: GeneralConfigState = {
  config: null,
  loading: false,
  saving: false,
  loadError: null,
  saveError: null
};

@Injectable()
export class GeneralConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    
    /**
     * Load the general configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, loadError: null, saveError: null })),
        switchMap(() => configService.getGeneralConfig().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false, loadError: null }),
            error: (error) => {
              const errorMessage = error.message || 'Failed to load configuration';
              patchState(store, { 
                loading: false, 
                loadError: errorMessage  // Only load errors should trigger "Not connected" state
              });
            }
          }),
          catchError((error) => {
            const errorMessage = error.message || 'Failed to load configuration';
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
     * Save the general configuration
     */
    saveConfig: rxMethod<GeneralConfig>(
      (config$: Observable<GeneralConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, saveError: null })),
        switchMap(config => configService.updateGeneralConfig(config).pipe(
          tap({
            next: () => {
              patchState(store, { 
                saving: false,
                saveError: null  // Clear any previous save errors
              });
            },
            error: (error) => {
              const errorMessage = error.message || 'Failed to save configuration';
              patchState(store, {
                saving: false,
                saveError: errorMessage  // Save errors should NOT trigger "Not connected" state
              });
            }
          }),
          catchError((error) => {
            const errorMessage = error.message || 'Failed to save configuration';
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
    updateConfigLocally(config: Partial<GeneralConfig>) {
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
     * Reset only save errors
     */
    resetSaveError() {
      patchState(store, { saveError: null });
    }
  })),
  withHooks({
    onInit({ loadConfig }) {
      loadConfig();
    }
  })
) {} 
