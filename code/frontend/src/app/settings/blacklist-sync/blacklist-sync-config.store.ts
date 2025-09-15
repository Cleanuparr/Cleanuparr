import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { ConfigurationService } from '../../core/services/configuration.service';
import { BlacklistSyncConfig } from '../../shared/models/blacklist-sync-config.model';
import { EMPTY, Observable } from 'rxjs';
import { switchMap, tap, catchError } from 'rxjs/operators';

export interface BlacklistSyncState {
  config: BlacklistSyncConfig | null;
  loading: boolean;
  saving: boolean;
  loadError: string | null;  // Only for load failures that should show "Not connected"
  saveError: string | null;  // Only for save failures that should show toast
}

const initialState: BlacklistSyncState = {
  config: null,
  loading: false,
  saving: false,
  loadError: null,
  saveError: null,
};

@Injectable()
export class BlacklistSyncConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, loadError: null, saveError: null })),
        switchMap(() => configService.getBlacklistSyncConfig().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false, loadError: null }),
            error: (error) => {
              const errorMessage = error.message || 'Failed to load Blacklist Sync configuration';
              patchState(store, { 
                loading: false, 
                loadError: errorMessage  // Only load errors should trigger "Not connected" state
              });
            }
          }),
          catchError((error) => {
            const errorMessage = error.message || 'Failed to load Blacklist Sync configuration';
            patchState(store, { 
              loading: false, 
              loadError: errorMessage  // Only load errors should trigger "Not connected" state
            });
            return EMPTY;
          })
        ))
      )
    ),
    saveConfig: rxMethod<BlacklistSyncConfig>(
      (config$: Observable<BlacklistSyncConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, saveError: null })),
        switchMap(config => configService.updateBlacklistSyncConfig(config).pipe(
          tap({
            next: () => patchState(store, { config, saving: false, saveError: null }),
            error: (error) => {
              const errorMessage = error.message || 'Failed to update Blacklist Sync configuration';
              patchState(store, { 
                saving: false, 
                saveError: errorMessage  // Save errors don't affect "Not connected" state
              });
            }
          }),
          catchError((error) => {
            const errorMessage = error.message || 'Failed to update Blacklist Sync configuration';
            patchState(store, { 
              saving: false, 
              saveError: errorMessage  // Save errors don't affect "Not connected" state
            });
            return EMPTY;
          })
        ))
      )
    ),
    updateConfigLocally(config: Partial<BlacklistSyncConfig>) {
      const current = store.config();
      if (current) {
        patchState(store, { config: { ...current, ...config } });
      }
    }
  })),
  withHooks({
    onInit({ loadConfig }) {
      loadConfig();
    }
  })
) {}
