import { Component, ChangeDetectionStrategy, input, output, model, HostListener } from '@angular/core';

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

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    this.close();
  }

  close(): void {
    this.visible.set(false);
    this.closed.emit();
  }

  onBackdropClick(): void {
    if (this.closeOnBackdrop()) {
      this.close();
    }
  }
}
