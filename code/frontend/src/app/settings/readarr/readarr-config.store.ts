import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { ReadarrConfig } from '../../shared/models/readarr-config.model';
import { ConfigurationService } from '../../core/services/configuration.service';
import { EMPTY, Observable, catchError, switchMap, tap, forkJoin, of } from 'rxjs';
import { ArrInstance, CreateArrInstanceDto, TestArrInstanceRequest } from '../../shared/models/arr-config.model';
import { TestConnectionResult } from '../../shared/models/download-client-config.model';

export interface ReadarrConfigState {
  config: ReadarrConfig | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
  instanceOperations: number;
  testing: boolean;
  testingInstanceId: string | null;
  testError: string | null;
  testResult: TestConnectionResult | null;
}

const initialState: ReadarrConfigState = {
  config: null,
  loading: false,
  saving: false,
  error: null,
  instanceOperations: 0,
  testing: false,
  testingInstanceId: null,
  testError: null,
  testResult: null
};

@Injectable()
export class ReadarrConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    
    /**
     * Load the Readarr configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() => configService.getReadarrConfig().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false }),
            error: (error) => {
              patchState(store, { 
                loading: false, 
                error: error.message || 'Failed to load Readarr configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Save the Readarr global configuration
     */
    saveConfig: rxMethod<{failedImportMaxStrikes: number}>(
      (globalConfig$: Observable<{failedImportMaxStrikes: number}>) => globalConfig$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(globalConfig => configService.updateReadarrConfig(globalConfig).pipe(
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
                error: error.message || 'Failed to save Readarr configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Save the Readarr configuration
     */
    saveFullConfig: rxMethod<ReadarrConfig>(
      (config$: Observable<ReadarrConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(config => configService.updateReadarrConfig(config).pipe(
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
                error: error.message || 'Failed to save Readarr configuration' 
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
    updateConfigLocally(config: Partial<ReadarrConfig>) {
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

    /**
     * Reset test state
     */
    resetTestState() {
      patchState(store, { testing: false, testingInstanceId: null, testError: null, testResult: null });
    },

    /**
     * Test a Readarr instance connection
     */
    testInstance: rxMethod<{ request: TestArrInstanceRequest; instanceId?: string }>(
      (params$: Observable<{ request: TestArrInstanceRequest; instanceId?: string }>) => params$.pipe(
        tap(({ instanceId }) => patchState(store, { testing: true, testingInstanceId: instanceId || null, testError: null, testResult: null })),
        switchMap(({ request }) => configService.testReadarrInstance(request).pipe(
          tap({
            next: (result) => {
              patchState(store, {
                testing: false,
                testResult: result,
                testError: null
              });
            },
            error: (error) => {
              patchState(store, {
                testing: false,
                testError: error.message || 'Connection test failed'
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    // ===== INSTANCE MANAGEMENT =====

    /**
     * Create a new Readarr instance
     */
    createInstance: rxMethod<CreateArrInstanceDto>(
      (instance$: Observable<CreateArrInstanceDto>) => instance$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(instance => configService.createReadarrInstance(instance).pipe(
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
                error: error.message || 'Failed to create Readarr instance' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Update a Readarr instance by ID
     */
    updateInstance: rxMethod<{ id: string, instance: CreateArrInstanceDto }>(
      (params$: Observable<{ id: string, instance: CreateArrInstanceDto }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(({ id, instance }) => configService.updateReadarrInstance(id, instance).pipe(
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
                error: error.message || `Failed to update Readarr instance with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Delete a Readarr instance by ID
     */
    deleteInstance: rxMethod<string>(
      (id$: Observable<string>) => id$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(id => configService.deleteReadarrInstance(id).pipe(
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
                error: error.message || `Failed to delete Readarr instance with ID ${id}` 
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
      config: ReadarrConfig,
      instanceOperations: {
        creates: CreateArrInstanceDto[],
        updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
        deletes: string[]
      }
    }>(
      (params$: Observable<{
        config: ReadarrConfig,
        instanceOperations: {
          creates: CreateArrInstanceDto[],
          updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
          deletes: string[]
        }
      }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(({ config, instanceOperations }) => {
          // First save the main config
          return configService.updateReadarrConfig(config).pipe(
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
                configService.createReadarrInstance(instance).pipe(
                  catchError(error => {
                    console.error('Failed to create Readarr instance:', error);
                    return of(null);
                  })
                )
              );
              
              const updateOps = updates.map(({ id, instance }) => 
                configService.updateReadarrInstance(id, instance).pipe(
                  catchError(error => {
                    console.error('Failed to update Readarr instance:', error);
                    return of(null);
                  })
                )
              );
              
              const deleteOps = deletes.map(id => 
                configService.deleteReadarrInstance(id).pipe(
                  catchError(error => {
                    console.error('Failed to delete Readarr instance:', error);
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
                error: error.message || 'Failed to save Readarr configuration' 
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