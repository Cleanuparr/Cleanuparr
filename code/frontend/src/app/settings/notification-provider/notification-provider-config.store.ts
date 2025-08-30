import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { 
  NotificationProvidersConfig, 
  NotificationProviderDto,
  TestNotificationResult
} from '../../shared/models/notification-provider.model';
import { NotificationProviderService } from '../../core/services/notification-provider.service';
import { NotificationProviderType } from '../../shared/models/enums';
import { EMPTY, Observable, catchError, switchMap, tap, forkJoin, of } from 'rxjs';

export interface NotificationProviderConfigState {
  config: NotificationProvidersConfig | null;
  loading: boolean;
  saving: boolean;
  testing: boolean;
  error: string | null;
  testResult: TestNotificationResult | null;
  pendingOperations: number;
}

const initialState: NotificationProviderConfigState = {
  config: null,
  loading: false,
  saving: false,
  testing: false,
  error: null,
  testResult: null,
  pendingOperations: 0
};

@Injectable()
export class NotificationProviderConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, notificationService = inject(NotificationProviderService)) => ({
    
    /**
     * Load the Notification Provider configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() => notificationService.getProviders().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false }),
            error: (error) => {
              patchState(store, { 
                loading: false, 
                error: error.message || 'Failed to load Notification Provider configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Create a new notification provider (provider-specific)
     */
    createProvider: rxMethod<{ provider: any, type: NotificationProviderType }>(
      (params$: Observable<{ provider: any, type: NotificationProviderType }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(({ provider, type }) => notificationService.createProvider(provider, type).pipe(
          tap({
            next: (newProvider) => {
              const currentConfig = store.config();
              if (currentConfig) {
                // Add the new provider to the providers array
                const updatedProviders = [...currentConfig.providers, newProvider];
                
                patchState(store, { 
                  config: { providers: updatedProviders },
                  saving: false 
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || 'Failed to create Notification Provider' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Update a specific notification provider by ID (provider-specific)
     */
    updateProvider: rxMethod<{ id: string, provider: any, type: NotificationProviderType }>(
      (params$: Observable<{ id: string, provider: any, type: NotificationProviderType }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(({ id, provider, type }) => notificationService.updateProvider(id, provider, type).pipe(
          tap({
            next: (updatedProvider) => {
              const currentConfig = store.config();
              if (currentConfig) {
                // Find and replace the updated provider in the providers array
                const updatedProviders = currentConfig.providers.map((p: NotificationProviderDto) => 
                  p.id === id ? updatedProvider : p
                );
                
                patchState(store, { 
                  config: { providers: updatedProviders },
                  saving: false 
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || `Failed to update Notification Provider with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Delete a notification provider by ID
     */
    deleteProvider: rxMethod<string>(
      (id$: Observable<string>) => id$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(id => notificationService.deleteProvider(id).pipe(
          tap({
            next: () => {
              const currentConfig = store.config();
              if (currentConfig) {
                // Remove the provider from the providers array
                const updatedProviders = currentConfig.providers.filter((p: NotificationProviderDto) => p.id !== id);
                
                patchState(store, { 
                  config: { providers: updatedProviders },
                  saving: false 
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || `Failed to delete Notification Provider with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Test a notification provider (provider-specific - without ID)
     */
    testProvider: rxMethod<{ testRequest: any, type: NotificationProviderType }>(
      (params$: Observable<{ testRequest: any, type: NotificationProviderType }>) => params$.pipe(
        tap(() => patchState(store, { testing: true, error: null, testResult: null })),
        switchMap(({ testRequest, type }) => notificationService.testProvider(testRequest, type).pipe(
          tap({
            next: (result) => {
              patchState(store, { 
                testing: false,
                testResult: result
              });
            },
            error: (error) => {
              patchState(store, { 
                testing: false, 
                error: error.message || 'Failed to test Notification Provider',
                testResult: {
                  success: false,
                  message: error.message || 'Test failed'
                }
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Batch create multiple providers (kept for compatibility)
     */
    createProviders: rxMethod<Array<{ provider: any, type: NotificationProviderType }>>(
      (providers$: Observable<Array<{ provider: any, type: NotificationProviderType }>>) => providers$.pipe(
        tap(() => patchState(store, { saving: true, error: null, pendingOperations: 0 })),
        switchMap(providers => {
          if (providers.length === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { pendingOperations: providers.length });
          
          // Create all providers in parallel
          const createOperations = providers.map(({ provider, type }) => 
            notificationService.createProvider(provider, type).pipe(
              catchError(error => {
                console.error('Failed to create provider:', error);
                return of(null); // Return null for failed operations
              })
            )
          );
          
          return forkJoin(createOperations).pipe(
            tap({
              next: (results) => {
                const currentConfig = store.config();
                if (currentConfig) {
                  // Filter out failed operations (null results)
                  const successfulProviders = results.filter(provider => provider !== null) as NotificationProviderDto[];
                  const updatedProviders = [...currentConfig.providers, ...successfulProviders];
                  
                  const failedCount = results.filter(provider => provider === null).length;
                  
                  patchState(store, { 
                    config: { providers: updatedProviders },
                    saving: false,
                    pendingOperations: 0,
                    error: failedCount > 0 ? `${failedCount} provider(s) failed to create` : null
                  });
                }
              },
              error: (error) => {
                patchState(store, { 
                  saving: false,
                  pendingOperations: 0,
                  error: error.message || 'Failed to create providers' 
                });
              }
            })
          );
        })
      )
    ),
    
    /**
     * Batch update multiple providers (kept for compatibility)
     */
    updateProviders: rxMethod<Array<{ id: string, provider: any, type: NotificationProviderType }>>(
      (updates$: Observable<Array<{ id: string, provider: any, type: NotificationProviderType }>>) => updates$.pipe(
        tap(() => patchState(store, { saving: true, error: null, pendingOperations: 0 })),
        switchMap(updates => {
          if (updates.length === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { pendingOperations: updates.length });
          
          // Update all providers in parallel
          const updateOperations = updates.map(({ id, provider, type }) => 
            notificationService.updateProvider(id, provider, type).pipe(
              catchError(error => {
                console.error('Failed to update provider:', error);
                return of(null); // Return null for failed operations
              })
            )
          );
          
          return forkJoin(updateOperations).pipe(
            tap({
              next: (results) => {
                const currentConfig = store.config();
                if (currentConfig) {
                  let updatedProviders = [...currentConfig.providers];
                  let failedCount = 0;
                  
                  // Update successful results
                  results.forEach((result, index) => {
                    if (result !== null) {
                      const providerIndex = updatedProviders.findIndex(p => p.id === updates[index].id);
                      if (providerIndex !== -1) {
                        updatedProviders[providerIndex] = result;
                      }
                    } else {
                      failedCount++;
                    }
                  });
                  
                  patchState(store, { 
                    config: { providers: updatedProviders },
                    saving: false,
                    pendingOperations: 0,
                    error: failedCount > 0 ? `${failedCount} provider(s) failed to update` : null
                  });
                }
              },
              error: (error) => {
                patchState(store, { 
                  saving: false,
                  pendingOperations: 0,
                  error: error.message || 'Failed to update providers' 
                });
              }
            })
          );
        })
      )
    ),
    
    /**
     * Update config in the store without saving to the backend
     */
    updateConfigLocally(config: Partial<NotificationProvidersConfig>) {
      const currentConfig = store.config();
      if (currentConfig) {
        patchState(store, {
          config: { ...currentConfig, ...config }
        });
      }
    },
    
    /**
     * Reset any errors and test results
     */
    resetError() {
      patchState(store, { error: null, testResult: null });
    },
    
    /**
     * Clear test result
     */
    clearTestResult() {
      patchState(store, { testResult: null });
    }
  })),
  withHooks({
    onInit({ loadConfig }) {
      loadConfig();
    }
  })
) {}
