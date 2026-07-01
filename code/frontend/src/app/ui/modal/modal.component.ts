import { Component, ChangeDetectionStrategy, input, output, model, HostListener } from '@angular/core';
import { registerOverlayEffect } from '@core/services/overlay-stack.service';

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

  private readonly isTopmostOverlay = registerOverlayEffect(this.visible);

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.visible() && this.isTopmostOverlay()) {
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
