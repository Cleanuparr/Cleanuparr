import { Injectable, signal, computed } from '@angular/core';

export type ToastSeverity = 'success' | 'error' | 'warning' | 'info';

export interface Toast {
  id: number;
  severity: ToastSeverity;
  message: string;
  duration: number;
}

const DEFAULT_DURATION = 4000;
const DEDUP_WINDOW = 300;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;
  private lastMessage = '';
  private lastMessageTime = 0;

  private readonly _toasts = signal<Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();
  readonly hasToasts = computed(() => this._toasts().length > 0);

  show(severity: ToastSeverity, message: string, duration = DEFAULT_DURATION): void {
    // Deduplicate rapid-fire same messages
    const now = Date.now();
    if (message === this.lastMessage && now - this.lastMessageTime < DEDUP_WINDOW) {
      return;
    }
    this.lastMessage = message;
    this.lastMessageTime = now;

    const id = this.nextId++;
    const toast: Toast = { id, severity, message, duration };

    this._toasts.update((toasts) => [...toasts, toast]);

    // Auto-dismiss
    setTimeout(() => this.dismiss(id), duration);
  }

  success(message: string): void {
    this.show('success', message);
  }

  error(message: string): void {
    this.show('error', message, 6000);
  }

  warning(message: string): void {
    this.show('warning', message, 5000);
  }

  info(message: string): void {
    this.show('info', message);
  }

  dismiss(id: number): void {
    this._toasts.update((toasts) => toasts.filter((t) => t.id !== id));
  }

  clear(): void {
    this._toasts.set([]);
  }
}
