import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { SonarrConfig } from '../../shared/models/sonarr-config.model';
import { ConfigurationService } from '../../core/services/configuration.service';
import { EMPTY, Observable, catchError, switchMap, tap, forkJoin, of } from 'rxjs';
import { ArrInstance, CreateArrInstanceDto, TestArrInstanceRequest } from '../../shared/models/arr-config.model';
import { TestConnectionResult } from '../../shared/models/download-client-config.model';

export interface SonarrConfigState {
  config: SonarrConfig | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
  instanceOperations: number;
  testing: boolean;
  testingInstanceId: string | null;
  testError: string | null;
  testResult: TestConnectionResult | null;
}

const initialState: SonarrConfigState = {
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
export class SonarrConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    
    /**
     * Load the Sonarr configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() => configService.getSonarrConfig().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false }),
            error: (error) => {
              patchState(store, { 
                loading: false, 
                error: error.message || 'Failed to load Sonarr configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Save the Sonarr global configuration
     */
    saveConfig: rxMethod<{failedImportMaxStrikes: number}>(
      (globalConfig$: Observable<{failedImportMaxStrikes: number}>) => globalConfig$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(globalConfig => configService.updateSonarrConfig(globalConfig).pipe(
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
                error: error.message || 'Failed to save Sonarr configuration' 
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
    updateConfigLocally(config: Partial<SonarrConfig>) {
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
     * Test a Sonarr instance connection
     */
    testInstance: rxMethod<{ request: TestArrInstanceRequest; instanceId?: string }>(
      (params$: Observable<{ request: TestArrInstanceRequest; instanceId?: string }>) => params$.pipe(
        tap(({ instanceId }) => patchState(store, { testing: true, testingInstanceId: instanceId || null, testError: null, testResult: null })),
        switchMap(({ request }) => configService.testSonarrInstance(request).pipe(
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
     * Create a new Sonarr instance
     */
    createInstance: rxMethod<CreateArrInstanceDto>(
      (instance$: Observable<CreateArrInstanceDto>) => instance$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(instance => configService.createSonarrInstance(instance).pipe(
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
                error: error.message || 'Failed to create Sonarr instance' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Update a Sonarr instance by ID
     */
    updateInstance: rxMethod<{ id: string, instance: CreateArrInstanceDto }>(
      (params$: Observable<{ id: string, instance: CreateArrInstanceDto }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(({ id, instance }) => configService.updateSonarrInstance(id, instance).pipe(
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
                error: error.message || `Failed to update Sonarr instance with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Delete a Sonarr instance by ID
     */
    deleteInstance: rxMethod<string>(
      (id$: Observable<string>) => id$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: store.instanceOperations() + 1 })),
        switchMap(id => configService.deleteSonarrInstance(id).pipe(
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
                error: error.message || `Failed to delete Sonarr instance with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),

    /**
     * Batch create multiple instances
     */
    createInstances: rxMethod<CreateArrInstanceDto[]>(
      (instances$: Observable<CreateArrInstanceDto[]>) => instances$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: 0 })),
        switchMap(instances => {
          if (instances.length === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { instanceOperations: instances.length });
          
          const createOperations = instances.map(instance => 
            configService.createSonarrInstance(instance).pipe(
              catchError(error => {
                console.error('Failed to create Sonarr instance:', error);
                return of(null);
              })
            )
          );
          
          return forkJoin(createOperations).pipe(
            tap({
              next: (results) => {
                const currentConfig = store.config();
                if (currentConfig) {
                  const successfulInstances = results.filter(instance => instance !== null) as ArrInstance[];
                  const updatedInstances = [...currentConfig.instances, ...successfulInstances];
                  
                  const failedCount = results.filter(instance => instance === null).length;
                  
                  patchState(store, { 
                    config: { ...currentConfig, instances: updatedInstances },
                    saving: false,
                    instanceOperations: 0,
                    error: failedCount > 0 ? `${failedCount} instance(s) failed to create` : null
                  });
                }
              },
              error: (error) => {
                patchState(store, { 
                  saving: false,
                  instanceOperations: 0,
                  error: error.message || 'Failed to create instances' 
                });
              }
            })
          );
        })
      )
    ),

    /**
     * Batch update multiple instances
     */
    updateInstances: rxMethod<Array<{ id: string, instance: CreateArrInstanceDto }>>(
      (updates$: Observable<Array<{ id: string, instance: CreateArrInstanceDto }>>) => updates$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: 0 })),
        switchMap(updates => {
          if (updates.length === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { instanceOperations: updates.length });
          
          const updateOperations = updates.map(({ id, instance }) => 
            configService.updateSonarrInstance(id, instance).pipe(
              catchError(error => {
                console.error('Failed to update Sonarr instance:', error);
                return of(null);
              })
            )
          );
          
          return forkJoin(updateOperations).pipe(
            tap({
              next: (results) => {
                const currentConfig = store.config();
                if (currentConfig) {
                  let updatedInstances = [...currentConfig.instances];
                  let failedCount = 0;
                  
                  results.forEach((result, index) => {
                    if (result !== null) {
                      const instanceIndex = updatedInstances.findIndex(inst => inst.id === updates[index].id);
                      if (instanceIndex !== -1) {
                        updatedInstances[instanceIndex] = result;
                      }
                    } else {
                      failedCount++;
                    }
                  });
                  
                  patchState(store, { 
                    config: { ...currentConfig, instances: updatedInstances },
                    saving: false,
                    instanceOperations: 0,
                    error: failedCount > 0 ? `${failedCount} instance(s) failed to update` : null
                  });
                }
              },
              error: (error) => {
                patchState(store, { 
                  saving: false,
                  instanceOperations: 0,
                  error: error.message || 'Failed to update instances' 
                });
              }
            })
          );
        })
      )
    ),

    /**
     * Process mixed operations (creates, updates, deletes) in batch
     */
    processBatchInstanceOperations: rxMethod<{
      creates: CreateArrInstanceDto[],
      updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
      deletes: string[]
    }>(
      (operations$: Observable<{
        creates: CreateArrInstanceDto[],
        updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
        deletes: string[]
      }>) => operations$.pipe(
        tap(() => patchState(store, { saving: true, error: null, instanceOperations: 0 })),
        switchMap(({ creates, updates, deletes }) => {
          const totalOperations = creates.length + updates.length + deletes.length;
          
          if (totalOperations === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { instanceOperations: totalOperations });
          
          // Prepare all operations
          const createOps = creates.map(instance => 
            configService.createSonarrInstance(instance).pipe(
              catchError(error => {
                console.error('Failed to create Sonarr instance:', error);
                return of(null);
              })
            )
          );
          
          const updateOps = updates.map(({ id, instance }) => 
            configService.updateSonarrInstance(id, instance).pipe(
              catchError(error => {
                console.error('Failed to update Sonarr instance:', error);
                return of(null);
              })
            )
          );
          
          const deleteOps = deletes.map(id => 
            configService.deleteSonarrInstance(id).pipe(
              catchError(error => {
                console.error('Failed to delete Sonarr instance:', error);
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
                  
                  // Process delete results (successful deletes are already handled by removing from array)
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
                  error: error.message || 'Failed to process operations' 
                });
              }
            })
          );
        })
      )
    ),

    /**
     * Save config and then process instance operations sequentially
     */
    saveConfigAndInstances: rxMethod<{
      config: SonarrConfig,
      instanceOperations: {
        creates: CreateArrInstanceDto[],
        updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
        deletes: string[]
      }
    }>(
      (params$: Observable<{
        config: SonarrConfig,
        instanceOperations: {
          creates: CreateArrInstanceDto[],
          updates: Array<{ id: string, instance: CreateArrInstanceDto }>,
          deletes: string[]
        }
      }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(({ config, instanceOperations }) => {
          // First save the main config
          return configService.updateSonarrConfig(config).pipe(
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
                configService.createSonarrInstance(instance).pipe(
                  catchError(error => {
                    console.error('Failed to create Sonarr instance:', error);
                    return of(null);
                  })
                )
              );
              
              const updateOps = updates.map(({ id, instance }) => 
                configService.updateSonarrInstance(id, instance).pipe(
                  catchError(error => {
                    console.error('Failed to update Sonarr instance:', error);
                    return of(null);
                  })
                )
              );
              
              const deleteOps = deletes.map(id => 
                configService.deleteSonarrInstance(id).pipe(
                  catchError(error => {
                    console.error('Failed to delete Sonarr instance:', error);
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
                error: error.message || 'Failed to save Sonarr configuration' 
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
