import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
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

  dismiss(toast: Toast): void {
    this.toastService.dismiss(toast.id);
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
