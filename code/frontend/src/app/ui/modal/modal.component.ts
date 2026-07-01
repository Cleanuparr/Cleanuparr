import { Component, ChangeDetectionStrategy, input, output, model, effect, inject, DestroyRef, HostListener } from '@angular/core';
import { OverlayStackService } from '@core/services/overlay-stack.service';

@Component({
  selector: 'app-modal',
  standalone: true,
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModalComponent {
  title = input<string>();
  size = input<'sm' | 'md' | 'lg'>('md');
  visible = model(false);
  closeOnBackdrop = input(true);

  closed = output<void>();

  private readonly overlays = inject(OverlayStackService);
  private overlayId: number | null = null;

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.overlayId ??= this.overlays.register();
      } else if (this.overlayId !== null) {
        this.overlays.unregister(this.overlayId);
        this.overlayId = null;
      }
    });
    inject(DestroyRef).onDestroy(() => {
      if (this.overlayId !== null) {
        this.overlays.unregister(this.overlayId);
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.visible() && this.overlayId !== null && this.overlays.isTopmost(this.overlayId)) {
      this.close();
    }
  }

  close(): void {
    this.visible.set(false);
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget && this.closeOnBackdrop()) {
      this.close();
    }
  }
}
