import { Injectable, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { ClientConfig, DownloadClientConfig, CreateDownloadClientDto, TestDownloadClientRequest, TestConnectionResult } from '../../shared/models/download-client-config.model';
import { ConfigurationService } from '../../core/services/configuration.service';
import { EMPTY, Observable, catchError, switchMap, tap, forkJoin, of } from 'rxjs';

export interface DownloadClientConfigState {
  config: DownloadClientConfig | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
  pendingOperations: number;
  testing: boolean;
  testingClientId: string | null;
  testError: string | null;
  testResult: TestConnectionResult | null;
}

const initialState: DownloadClientConfigState = {
  config: null,
  loading: false,
  saving: false,
  error: null,
  pendingOperations: 0,
  testing: false,
  testingClientId: null,
  testError: null,
  testResult: null
};

@Injectable()
export class DownloadClientConfigStore extends signalStore(
  withState(initialState),
  withMethods((store, configService = inject(ConfigurationService)) => ({
    
    /**
     * Load the Download Client configuration
     */
    loadConfig: rxMethod<void>(
      pipe => pipe.pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() => configService.getDownloadClientConfig().pipe(
          tap({
            next: (config) => patchState(store, { config, loading: false }),
            error: (error) => {
              patchState(store, { 
                loading: false, 
                error: error.message || 'Failed to load Download Client configuration' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Save the Download Client configuration
     */
    saveConfig: rxMethod<DownloadClientConfig>(
      (config$: Observable<DownloadClientConfig>) => config$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(config => configService.updateDownloadClientConfig(config).pipe(
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
                error: error.message || 'Failed to save Download Client configuration' 
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
    updateConfigLocally(config: Partial<DownloadClientConfig>) {
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
      patchState(store, { testing: false, testingClientId: null, testError: null, testResult: null });
    },

    /**
     * Test a download client connection
     */
    testClient: rxMethod<{ request: TestDownloadClientRequest; clientId?: string }>(
      (params$: Observable<{ request: TestDownloadClientRequest; clientId?: string }>) => params$.pipe(
        tap(({ clientId }) => patchState(store, { testing: true, testingClientId: clientId || null, testError: null, testResult: null })),
        switchMap(({ request }) => configService.testDownloadClient(request).pipe(
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

    /**
     * Batch create multiple clients
     */
    createClients: rxMethod<CreateDownloadClientDto[]>(
      (clients$: Observable<CreateDownloadClientDto[]>) => clients$.pipe(
        tap(() => patchState(store, { saving: true, error: null, pendingOperations: 0 })),
        switchMap(clients => {
          if (clients.length === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { pendingOperations: clients.length });
          
          // Create all clients in parallel
          const createOperations = clients.map(client => 
            configService.createDownloadClient(client).pipe(
              catchError(error => {
                console.error('Failed to create client:', error);
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
                  const successfulClients = results.filter(client => client !== null) as ClientConfig[];
                  const updatedClients = [...currentConfig.clients, ...successfulClients];
                  
                  const failedCount = results.filter(client => client === null).length;
                  
                  patchState(store, { 
                    config: { clients: updatedClients },
                    saving: false,
                    pendingOperations: 0,
                    error: failedCount > 0 ? `${failedCount} client(s) failed to create` : null
                  });
                }
              },
              error: (error) => {
                patchState(store, { 
                  saving: false,
                  pendingOperations: 0,
                  error: error.message || 'Failed to create clients' 
                });
              }
            })
          );
        })
      )
    ),
    
    /**
     * Create a new download client
     */
    createClient: rxMethod<CreateDownloadClientDto>(
      (client$: Observable<CreateDownloadClientDto>) => client$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(client => configService.createDownloadClient(client).pipe(
          tap({
            next: (newClient) => {
              const currentConfig = store.config();
              if (currentConfig) {
                // Add the new client to the clients array
                const updatedClients = [...currentConfig.clients, newClient];
                
                patchState(store, { 
                  config: { clients: updatedClients },
                  saving: false 
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || 'Failed to create Download Client' 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Batch update multiple clients
     */
    updateClients: rxMethod<Array<{ id: string, client: ClientConfig }>>(
      (updates$: Observable<Array<{ id: string, client: ClientConfig }>>) => updates$.pipe(
        tap(() => patchState(store, { saving: true, error: null, pendingOperations: 0 })),
        switchMap(updates => {
          if (updates.length === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { pendingOperations: updates.length });
          
          // Update all clients in parallel
          const updateOperations = updates.map(({ id, client }) => 
            configService.updateDownloadClient(id, client).pipe(
              catchError(error => {
                console.error('Failed to update client:', error);
                return of(null); // Return null for failed operations
              })
            )
          );
          
          return forkJoin(updateOperations).pipe(
            tap({
              next: (results) => {
                const currentConfig = store.config();
                if (currentConfig) {
                  let updatedClients = [...currentConfig.clients];
                  let failedCount = 0;
                  
                  // Update successful results
                  results.forEach((result, index) => {
                    if (result !== null) {
                      const clientIndex = updatedClients.findIndex(c => c.id === updates[index].id);
                      if (clientIndex !== -1) {
                        updatedClients[clientIndex] = result;
                      }
                    } else {
                      failedCount++;
                    }
                  });
                  
                  patchState(store, { 
                    config: { clients: updatedClients },
                    saving: false,
                    pendingOperations: 0,
                    error: failedCount > 0 ? `${failedCount} client(s) failed to update` : null
                  });
                }
              },
              error: (error) => {
                patchState(store, { 
                  saving: false,
                  pendingOperations: 0,
                  error: error.message || 'Failed to update clients' 
                });
              }
            })
          );
        })
      )
    ),
    
    /**
     * Update a specific download client by ID
     */
    updateClient: rxMethod<{ id: string, client: ClientConfig }>(
      (params$: Observable<{ id: string, client: ClientConfig }>) => params$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(({ id, client }) => configService.updateDownloadClient(id, client).pipe(
          tap({
            next: (updatedClient) => {
              const currentConfig = store.config();
              if (currentConfig) {
                // Find and replace the updated client in the clients array
                const updatedClients = currentConfig.clients.map((c: ClientConfig) => 
                  c.id === id ? updatedClient : c
                );
                
                patchState(store, { 
                  config: { clients: updatedClients },
                  saving: false 
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || `Failed to update Download Client with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Delete a download client by ID
     */
    deleteClient: rxMethod<string>(
      (id$: Observable<string>) => id$.pipe(
        tap(() => patchState(store, { saving: true, error: null })),
        switchMap(id => configService.deleteDownloadClient(id).pipe(
          tap({
            next: () => {
              const currentConfig = store.config();
              if (currentConfig) {
                // Remove the client from the clients array
                const updatedClients = currentConfig.clients.filter((c: ClientConfig) => c.id !== id);
                
                patchState(store, { 
                  config: { clients: updatedClients },
                  saving: false 
                });
              }
            },
            error: (error) => {
              patchState(store, { 
                saving: false, 
                error: error.message || `Failed to delete Download Client with ID ${id}` 
              });
            }
          }),
          catchError(() => EMPTY)
        ))
      )
    ),
    
    /**
     * Process mixed operations (creates and updates) in batch
     */
    processBatchOperations: rxMethod<{
      creates: CreateDownloadClientDto[],
      updates: Array<{ id: string, client: ClientConfig }>
    }>(
      (operations$: Observable<{
        creates: CreateDownloadClientDto[],
        updates: Array<{ id: string, client: ClientConfig }>
      }>) => operations$.pipe(
        tap(() => patchState(store, { saving: true, error: null, pendingOperations: 0 })),
        switchMap(({ creates, updates }) => {
          const totalOperations = creates.length + updates.length;
          
          if (totalOperations === 0) {
            patchState(store, { saving: false });
            return EMPTY;
          }
          
          patchState(store, { pendingOperations: totalOperations });
          
          // Prepare all operations
          const createOps = creates.map(client => 
            configService.createDownloadClient(client).pipe(
              catchError(error => {
                console.error('Failed to create client:', error);
                return of(null);
              })
            )
          );
          
          const updateOps = updates.map(({ id, client }) => 
            configService.updateDownloadClient(id, client).pipe(
              catchError(error => {
                console.error('Failed to update client:', error);
                return of(null);
              })
            )
          );
          
          // Execute all operations in parallel
          return forkJoin([...createOps, ...updateOps]).pipe(
            tap({
              next: (results) => {
                const currentConfig = store.config();
                if (currentConfig) {
                  let updatedClients = [...currentConfig.clients];
                  let failedCount = 0;
                  
                  // Process create results
                  const createResults = results.slice(0, creates.length);
                  const successfulCreates = createResults.filter(client => client !== null) as ClientConfig[];
                  updatedClients = [...updatedClients, ...successfulCreates];
                  failedCount += createResults.filter(client => client === null).length;
                  
                  // Process update results
                  const updateResults = results.slice(creates.length);
                  updateResults.forEach((result, index) => {
                    if (result !== null) {
                      const clientIndex = updatedClients.findIndex(c => c.id === updates[index].id);
                      if (clientIndex !== -1) {
                        updatedClients[clientIndex] = result as ClientConfig;
                      }
                    } else {
                      failedCount++;
                    }
                  });
                  
                  patchState(store, { 
                    config: { clients: updatedClients },
                    saving: false,
                    pendingOperations: 0,
                    error: failedCount > 0 ? `${failedCount} operation(s) failed` : null
                  });
                }
              },
              error: (error) => {
                patchState(store, { 
                  saving: false,
                  pendingOperations: 0,
                  error: error.message || 'Failed to process operations' 
                });
              }
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
