import { Injectable, inject, signal, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { SignalRHubConfig } from '@core/models/signalr.models';
import { ApplicationPathService } from '@core/services/base-path.service';
import { AuthService } from '@core/auth/auth.service';

@Injectable()
export abstract class HubService implements OnDestroy {
  private readonly pathService = inject(ApplicationPathService);
  private readonly authService = inject(AuthService);
  private connection: signalR.HubConnection | null = null;
  private reconnectAttempts = 0;
  private reconnectTimeout: ReturnType<typeof setTimeout> | null = null;

  protected readonly connected = signal(false);

  readonly isConnected = this.connected.asReadonly();

  protected abstract readonly config: SignalRHubConfig;

  protected abstract registerHandlers(connection: signalR.HubConnection): void;

  async start(): Promise<void> {
    if (this.connection) return;

    const hubUrl = this.pathService.buildHubUrl(this.config.hubUrl);

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.authService.getAccessToken() ?? '',
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          return Math.min(
            this.config.reconnectDelayMs * Math.pow(2, retryContext.previousRetryCount),
            30_000,
          );
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.onreconnecting(() => {
      this.connected.set(false);
    });

    this.connection.onreconnected(() => {
      this.connected.set(true);
      this.reconnectAttempts = 0;
      this.onReconnected();
    });

    this.connection.onclose(() => {
      this.connected.set(false);
      this.scheduleReconnect();
    });

    this.registerHandlers(this.connection);

    try {
      await this.connection.start();
      this.connected.set(true);
      this.reconnectAttempts = 0;
      this.onConnected();
    } catch {
      this.scheduleReconnect();
    }
  }

  async stop(): Promise<void> {
    if (this.reconnectTimeout) {
      clearTimeout(this.reconnectTimeout);
      this.reconnectTimeout = null;
    }
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
    this.connected.set(false);
  }

  protected invoke(method: string, ...args: unknown[]): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return Promise.resolve();
    }
    return this.connection.invoke(method, ...args);
  }

  protected onConnected(): void {}
  protected onReconnected(): void {}

  ngOnDestroy(): void {
    this.stop();
  }

  private scheduleReconnect(): void {
    const maxAttempts = this.config.maxReconnectAttempts;
    if (maxAttempts > 0 && this.reconnectAttempts >= maxAttempts) return;

    const delay = Math.min(
      this.config.reconnectDelayMs * Math.pow(2, this.reconnectAttempts),
      30_000,
    );
    this.reconnectAttempts++;

    this.reconnectTimeout = setTimeout(() => {
      this.connection = null;
      this.start();
    }, delay);
  }
}
