import { Component, ChangeDetectionStrategy, input, output, model, HostListener, effect, ElementRef, inject, OnInit, OnDestroy } from '@angular/core';
import { A11yModule } from '@angular/cdk/a11y';

@Component({
  selector: 'app-drawer',
  standalone: true,
  imports: [A11yModule],
  templateUrl: './drawer.component.html',
  styleUrl: './drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DrawerComponent implements OnInit, OnDestroy {
  private static nextId = 0;

  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private previousFocus: HTMLElement | null = null;

  readonly titleId = `drawer-title-${++DrawerComponent.nextId}`;

  title = input<string>();
  visible = model(false);
  closeOnBackdrop = input(true);

  closed = output<void>();

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.previousFocus = document.activeElement instanceof HTMLElement
          ? document.activeElement
          : null;
        queueMicrotask(() => this.focusFirstControl());
      }
    });
  }

  ngOnInit(): void {
    document.body.appendChild(this.host.nativeElement);
  }

  ngOnDestroy(): void {
    this.restoreFocus();
    this.host.nativeElement.remove();
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.visible()) {
      this.close();
    }
  }

  close(): void {
    this.visible.set(false);
    this.restoreFocus();
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

  private restoreFocus(): void {
    const target = this.previousFocus;
    this.previousFocus = null;
    if (target && document.body.contains(target)) {
      target.focus();
    }
  }
}
