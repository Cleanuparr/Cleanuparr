import { Component, ChangeDetectionStrategy, input, output, model, HostListener, effect, ElementRef, inject, OnInit, OnDestroy } from '@angular/core';
import { A11yModule } from '@angular/cdk/a11y';
import { generateControlId } from '@ui/control-id';
import { registerOverlayEffect } from '@core/services/overlay-stack.service';

@Component({
  selector: 'app-drawer',
  standalone: true,
  imports: [A11yModule],
  templateUrl: './drawer.component.html',
  styleUrl: './drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DrawerComponent implements OnInit, OnDestroy {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private previousFocus: HTMLElement | null = null;

  readonly titleId = generateControlId('drawer-title');

  title = input<string>();
  visible = model(false);
  closeOnBackdrop = input(true);

  closed = output<void>();

  private readonly isTopmostOverlay = registerOverlayEffect(this.visible);

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
    if (this.visible() && this.isTopmostOverlay()) {
      this.close();
    }
  }

  close(): void {
    this.visible.set(false);
    this.restoreFocus();
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget && this.closeOnBackdrop()) {
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
