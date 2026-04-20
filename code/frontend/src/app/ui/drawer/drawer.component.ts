import { Component, ChangeDetectionStrategy, input, output, model, HostListener, effect, ElementRef, inject } from '@angular/core';

@Component({
  selector: 'app-drawer',
  standalone: true,
  templateUrl: './drawer.component.html',
  styleUrl: './drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DrawerComponent {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  title = input<string>();
  visible = model(false);
  closeOnBackdrop = input(true);

  closed = output<void>();

  constructor() {
    effect(() => {
      if (this.visible()) {
        queueMicrotask(() => this.focusFirstControl());
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.visible()) {
      this.close();
    }
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

  private focusFirstControl(): void {
    const panel = this.host.nativeElement.querySelector('.drawer__body') as HTMLElement | null;
    if (!panel) return;
    const focusable = panel.querySelector(
      'input, select, textarea, button, [tabindex]:not([tabindex="-1"])'
    ) as HTMLElement | null;
    focusable?.focus();
  }
}
