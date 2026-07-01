import { Component, ChangeDetectionStrategy, inject, effect, HostListener } from '@angular/core';
import { ConfirmService } from '@core/services/confirm.service';
import { OverlayStackService } from '@core/services/overlay-stack.service';
import { ButtonComponent } from '../button/button.component';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [ButtonComponent],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  readonly confirm = inject(ConfirmService);

  private readonly overlays = inject(OverlayStackService);
  private overlayId: number | null = null;

  constructor() {
    effect(() => {
      if (this.confirm.state()) {
        this.overlayId ??= this.overlays.register();
      } else if (this.overlayId !== null) {
        this.overlays.unregister(this.overlayId);
        this.overlayId = null;
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.confirm.state() && this.overlayId !== null && this.overlays.isTopmost(this.overlayId)) {
      this.confirm.cancel();
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.confirm.cancel();
    }
  }
}
