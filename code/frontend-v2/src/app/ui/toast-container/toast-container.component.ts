import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { ToastService, type Toast } from '@core/services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [NgIcon],
  templateUrl: './toast-container.component.html',
  styleUrl: './toast-container.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastContainerComponent {
  private readonly toastService = inject(ToastService);
  readonly toasts = this.toastService.toasts;
  readonly removingIds = signal<Set<number>>(new Set());

  dismiss(toast: Toast): void {
    // Mark as removing to trigger exit animation
    this.removingIds.update((ids) => new Set(ids).add(toast.id));

    // Remove after animation completes (300ms)
    setTimeout(() => {
      this.toastService.dismiss(toast.id);
      this.removingIds.update((ids) => {
        const next = new Set(ids);
        next.delete(toast.id);
        return next;
      });
    }, 300);
  }

  isRemoving(id: number): boolean {
    return this.removingIds().has(id);
  }

  iconFor(severity: string): string {
    switch (severity) {
      case 'success': return 'tablerCheck';
      case 'error': return 'tablerBan';
      case 'warning': return 'tablerBellRinging';
      case 'info': return 'tablerBell';
      default: return 'tablerBell';
    }
  }
}
