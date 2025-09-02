import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { SignalRHubConfig } from '../models/signalr.models';

/**
 * Base service for SignalR hub connections.
 * Provides common functionality for connecting to, monitoring, and managing SignalR hubs.
 * 
 * @typeParam T - The type of messages that will be received from this hub
 */
@Injectable()
export abstract class BaseSignalRService<T> implements OnDestroy {
  protected hubConnection!: signalR.HubConnection;
  protected connectionStatusSubject = new BehaviorSubject<boolean>(false);
  protected messageSubject = new BehaviorSubject<T[]>([]);
  protected destroy$ = new Subject<void>();
  protected reconnectAttempts = 0;
  protected connectionHealthCheckInterval: any;
  protected messageBuffer: T[] = [];
  
  /**
   * Initialize a base SignalR hub connection service
   * 
   * @param config - Configuration for the hub connection
   * @param messageEventName - The name of the event that delivers messages from the server
   */
  constructor(
    protected config: SignalRHubConfig,
    protected messageEventName: string
  ) {}
  
  /**
   * Start the SignalR connection to the hub
   * @returns Promise that resolves when the connection is established
   */
  public startConnection(): Promise<void> {
    if (this.hubConnection && 
        this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return Promise.resolve();
    }

    // Build a new connection if needed
    if (!this.hubConnection) {
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(this.config.hubUrl)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (this.config.maxReconnectAttempts > 0 && 
                retryContext.previousRetryCount >= this.config.maxReconnectAttempts) {
              return null; // Stop trying after max attempts
            }
            
            // Implement exponential backoff with a maximum delay of 30 seconds
            return Math.min(
              this.config.reconnectDelayMs * Math.pow(2, retryContext.previousRetryCount), 
              30000
            );
          }
        })
        .build();

      this.registerSignalREvents();
    }

    // Start health check interval if configured
    this.startHealthCheck();

    return this.hubConnection.start()
      .then(() => {
        this.connectionStatusSubject.next(true);
        this.reconnectAttempts = 0;
        this.onConnectionEstablished();
      })
      .catch(err => {
        console.error(`Error connecting to ${this.config.hubUrl}:`, err);
        this.connectionStatusSubject.next(false);
        
        // Always try to reconnect, optionally limited by maxReconnectAttempts
        const infiniteReconnect = this.config.maxReconnectAttempts === 0;
        if (infiniteReconnect || this.reconnectAttempts < this.config.maxReconnectAttempts) {
          this.reconnectAttempts++;
          const delay = Math.min(
            this.config.reconnectDelayMs * Math.pow(1.5, this.reconnectAttempts), 
            30000
          );
          
          setTimeout(() => this.startConnection(), delay);
        }
        
        throw err;
      });
  }

  /**
   * Stop the SignalR connection
   * @returns Promise that resolves when the connection is stopped
   */
  public stopConnection(): Promise<void> {
    this.stopHealthCheck();
    
    if (!this.hubConnection) {
      return Promise.resolve();
    }

    return this.hubConnection.stop()
      .then(() => {
        this.connectionStatusSubject.next(false);
      })
      .catch(err => {
        throw err;
      });
  }

  /**
   * Register event handlers for SignalR events
   */
  protected registerSignalREvents(): void {
    // Handle incoming messages
    this.hubConnection.on(this.messageEventName, (message: T) => {
      this.processMessage(message);
    });

    // Handle reconnection events
    this.hubConnection.onreconnected(() => {
      this.connectionStatusSubject.next(true);
      this.reconnectAttempts = 0;
      this.onConnectionEstablished();
    });

    this.hubConnection.onreconnecting(() => {
      this.connectionStatusSubject.next(false);
    });

    this.hubConnection.onclose(() => {
      this.connectionStatusSubject.next(false);
      
      // Try to reconnect if the connection was closed unexpectedly
      if (this.shouldAttemptReconnect()) {
        this.startConnection();
      }
    });
  }

  /**
   * Start the health check timer to periodically verify connection status
   */
  protected startHealthCheck(): void {
    this.stopHealthCheck();
    
    if (this.config.healthCheckIntervalMs > 0) {
      this.connectionHealthCheckInterval = setInterval(() => {
        this.checkConnectionHealth();
      }, this.config.healthCheckIntervalMs);
    }
  }

  /**
   * Stop the health check timer
   */
  protected stopHealthCheck(): void {
    if (this.connectionHealthCheckInterval) {
      clearInterval(this.connectionHealthCheckInterval);
      this.connectionHealthCheckInterval = null;
    }
  }

  /**
   * Check the health of the connection and attempt to reconnect if needed
   */
  protected checkConnectionHealth(): void {
    if (!this.hubConnection || 
        this.hubConnection.state === signalR.HubConnectionState.Disconnected) {
      this.startConnection();
    }
  }

  /**
   * Process a message received from the hub
   * @param message - The message received from the hub
   */
  protected processMessage(message: T): void {
    // Add to buffer
    this.addToBuffer(message);
    
    // Update the subject with current messages
    const currentMessages = this.messageSubject.value;
    this.messageSubject.next([...currentMessages, message]);
  }

  /**
   * Add a message to the buffer
   * @param message - The message to add to the buffer
   */
  protected addToBuffer(message: T): void {
    this.messageBuffer.push(message);
    
    // Trim buffer if it exceeds the limit
    if (this.messageBuffer.length > this.config.bufferSize) {
      this.messageBuffer.shift();
    }
  }

  /**
   * Called when a connection is established or re-established
   * Override in derived classes to perform hub-specific initialization
   */
  protected onConnectionEstablished(): void {
    // Override in derived classes
  }

  /**
   * Determine if a reconnection attempt should be made
   * @returns True if a reconnection attempt should be made
   */
  protected shouldAttemptReconnect(): boolean {
    // By default, always try to reconnect unless max attempts reached
    return this.config.maxReconnectAttempts === 0 || 
           this.reconnectAttempts < this.config.maxReconnectAttempts;
  }

  /**
   * Force a reconnection attempt, even if max attempts have been reached
   */
  public forceReconnect(): void {
    this.reconnectAttempts = 0;
    this.startConnection();
  }

  /**
   * Get the buffered messages
   * @returns A copy of the message buffer
   */
  public getBufferedMessages(): T[] {
    return [...this.messageBuffer];
  }

  /**
   * Get messages as an observable
   * @returns An observable that emits the current messages and all future messages
   */
  public getMessages(): Observable<T[]> {
    return this.messageSubject.asObservable();
  }

  /**
   * Get connection status as an observable
   * @returns An observable that emits the current connection status and all future status changes
   */
  public getConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }

  /**
   * Clean up resources
   */
  ngOnDestroy(): void {
    this.stopHealthCheck();
    this.stopConnection();
  }
}
