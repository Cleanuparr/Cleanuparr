import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { WhisparrConfig } from '../../shared/models/whisparr-config.model';
import { ConfigurationService } from '../../core/services/configuration.service';
import { EMPTY, Observable, catchError, switchMap, tap, forkJoin, of } from 'rxjs';
import { ArrInstance, CreateArrInstanceDto } from '../../shared/models/arr-config.model';

export interface WhisparrConfigState {
  config: WhisparrConfig | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
  instanceOperations: number;
}

const initialState: WhisparrConfigState = {
  config: null,
  loading: false,
  saving: false,
  error: null,
  instanceOperations: 0
};

@Injectable()
export class WhisparrConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    
    /**
     * Load the Whisparr configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() => configService.getWhisparrConfig().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false }),
            error: (error) => {
              patchState(store, { 
                loading: false, 
                error: error.message || 'Failed to load Whisparr configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Save the Whisparr global configuration
     */
    saveConfig: rxMethod<{failedImportMaxStrikes: number}>(
      (globalConfig$: Observable<{failedImportMaxStrikes: number}>) => globalConfig$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(globalConfig => configService.updateWhisparrConfig(globalConfig).pipe(
          tap({
            next: () => {
              const currentConfig = store.config();
              if (currentConfig) {
                // Update the local config with the new global settings
                patchState(store, { 
                  config: { ...currentConfig, ...globalConfig }, 
                  saving: false 
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || 'Failed to save Whisparr configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Save the Whisparr configuration
     */
    saveFullConfig: rxMethod<WhisparrConfig>(
      (config$: Observable<WhisparrConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(config => configService.updateWhisparrConfig(config).pipe(
          tap({
            next: () => {
              patchState(store, { 
                config, 
                saving: false 
              });
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || 'Failed to save Whisparr configuration' 
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
    updateConfigLocally(config: Partial<WhisparrConfig>) {
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
    },

    // ===== INSTANCE MANAGEMENT =====

    /**
     * Create a new Whisparr instance
     */
    createInstance: rxMethod<CreateArrInstanceDto>(
      (instance$: Observable<CreateArrInstanceDto>) => instance$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(instance => configService.createWhisparrInstance(instance).pipe(
          tap({
            next: (newInstance) => {
              const currentConfig = store.config();
              if (currentConfig) {
                patchState(store, { 
                  config: { ...currentConfig, instances: [...currentConfig.instances, newInstance] },
                  saving: false,
                  instanceOperations: store.instanceOperations() - 1
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false,
                instanceOperations: store.instanceOperations() - 1,
                error: error.message || 'Failed to create Whisparr instance' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Update a Whisparr instance by ID
     */
    updateInstance: rxMethod<{ id: string, instance: CreateArrInstanceDto }>(
      (params$: Observable<{ id: string, instance: CreateArrInstanceDto }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(({ id, instance }) => configService.updateWhisparrInstance(id, instance).pipe(
          tap({
            next: (updatedInstance) => {
              const currentConfig = store.config();
              if (currentConfig) {
                const updatedInstances = currentConfig.instances.map((inst: ArrInstance) => 
                  inst.id === id ? updatedInstance : inst
                );
                patchState(store, { 
                  config: { ...currentConfig, instances: updatedInstances },
                  saving: false,
                  instanceOperations: store.instanceOperations() - 1
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false,
                instanceOperations: store.instanceOperations() - 1,
                error: error.message || `Failed to update Whisparr instance with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Delete a Whisparr instance by ID
     */
    deleteInstance: rxMethod<string>(
      (id$: Observable<string>) => id$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(id => configService.deleteWhisparrInstance(id).pipe(
          tap({
            next: () => {
              const currentConfig = store.config();
              if (currentConfig) {
                const updatedInstances = currentConfig.instances.filter((inst: ArrInstance) => inst.id !== id);
                patchState(store, { 
                  config: { ...currentConfig, instances: updatedInstances },
                  saving: false,
                  instanceOperations: store.instanceOperations() - 1
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false,
                instanceOperations: store.instanceOperations() - 1,
                error: error.message || `Failed to delete Whisparr instance with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Save config and then process instance operations sequentially
     */
    saveConfigAndInstances: rxMethod<{
      config: WhisparrConfig,
      instanceOperations: {
        creates: CreateArrInstanceDto[],
        updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
        deletes: string[]
      }
    }>(
      (params$: Observable<{
        config: WhisparrConfig,
        instanceOperations: {
          creates: CreateArrInstanceDto[],
          updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
          deletes: string[]
        }
      }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(({ config, instanceOperations }) => {
          // First save the main config
          return configService.updateWhisparrConfig(config).pipe(
            tap(() => {
              patchState(store, { config });
            }),
            switchMap(() => {
              // Then process instance operations if any
              const { creates, updates, deletes } = instanceOperations;
              const totalOperations = creates.length + updates.length + deletes.length;
              
              if (totalOperations === 0) {
                patchState(store, { saving: false });
                return EMPTY;
              }
              
              patchState(store, { instanceOperations: totalOperations });
              
              // Prepare all operations
              const createOps = creates.map(instance => 
                configService.createWhisparrInstance(instance).pipe(
                  catchError(error => {
                    console.error('Failed to create Whisparr instance:', error);
                    return of(null);
                  })
                )
              );
              
              const updateOps = updates.map(({ id, instance }) => 
                configService.updateWhisparrInstance(id, instance).pipe(
                  catchError(error => {
                    console.error('Failed to update Whisparr instance:', error);
                    return of(null);
                  })
                )
              );
              
              const deleteOps = deletes.map(id => 
                configService.deleteWhisparrInstance(id).pipe(
                  catchError(error => {
                    console.error('Failed to delete Whisparr instance:', error);
                    return of(null);
                  })
                )
              );
              
              // Execute all operations in parallel
              return forkJoin([...createOps, ...updateOps, ...deleteOps]).pipe(
                tap({
                  next: (results) => {
                    const currentConfig = store.config();
                    if (currentConfig) {
                      let updatedInstances = [...currentConfig.instances];
                      let failedCount = 0;
                      
                      // Process create results
                      const createResults = results.slice(0, creates.length);
                      const successfulCreates = createResults.filter(instance => instance !== null) as ArrInstance[];
                      updatedInstances = [...updatedInstances, ...successfulCreates];
                      failedCount += createResults.filter(instance => instance === null).length;
                      
                      // Process update results
                      const updateResults = results.slice(creates.length, creates.length + updates.length);
                      updateResults.forEach((result, index) => {
                        if (result !== null) {
                          const instanceIndex = updatedInstances.findIndex(inst => inst.id === updates[index].id);
                          if (instanceIndex !== -1) {
                            updatedInstances[instanceIndex] = result as ArrInstance;
                          }
                        } else {
                          failedCount++;
                        }
                      });
                      
                      // Process delete results
                      const deleteResults = results.slice(creates.length + updates.length);
                      deleteResults.forEach((result, index) => {
                        if (result !== null) {
                          // Delete was successful, remove from array
                          updatedInstances = updatedInstances.filter(inst => inst.id !== deletes[index]);
                        } else {
                          failedCount++;
                        }
                      });
                      
                      patchState(store, { 
                        config: { ...currentConfig, instances: updatedInstances },
                        saving: false,
                        instanceOperations: 0,
                        error: failedCount > 0 ? `${failedCount} operation(s) failed` : null
                      });
                    }
                  },
                  error: (error) => {
                    patchState(store, { 
                      saving: false,
                      instanceOperations: 0,
                      error: error.message || 'Failed to process instance operations' 
                    });
                  }
                })
              );
            }),
            catchError((error) => {
              patchState(store, { 
                saving: false,
                error: error.message || 'Failed to save Whisparr configuration' 
              });
              return EMPTY;
            })
          );
        })
      )
    )
  })),
  withHooks({
    onInit({ loadConfig }) {
      loadConfig();
    }
  })
) {} 