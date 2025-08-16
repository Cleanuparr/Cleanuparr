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
  error: string | null;
}

const initialState: GeneralConfigState = {
  config: null,
  loading: false,
  saving: false,
  error: null
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
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() => configService.getGeneralConfig().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false }),
            error: (error) => {
              patchState(store, { 
                loading: false, 
                error: error.message || 'Failed to load configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Save the general configuration
     */
    saveConfig: rxMethod<GeneralConfig>(
      (config$: Observable<GeneralConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(config => configService.updateGeneralConfig(config).pipe(
          tap({
            next: () => {
              patchState(store, { 
                saving: false,
                error: null  // Clear any previous save errors
              });
            },
            error: (error) => {
              patchState(store, {
                saving: false,
                error: error.message || 'Failed to save configuration'
              });
            }
          }),
          catchError(() => EMPTY)
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
      patchState(store, { error: null });
    }
  })),
  withHooks({
    onInit({ loadConfig }) {
      loadConfig();
    }
  })
) {} 
