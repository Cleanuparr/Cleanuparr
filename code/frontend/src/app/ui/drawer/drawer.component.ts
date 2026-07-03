import { Component, ChangeDetectionStrategy, input, output, model, HostListener, effect, inject, DestroyRef, viewChild } from '@angular/core';
import { A11yModule } from '@angular/cdk/a11y';
import { CdkPortal, PortalModule } from '@angular/cdk/portal';
import { Overlay, OverlayModule, OverlayRef } from '@angular/cdk/overlay';
import { generateControlId } from '@ui/control-id';
import { registerOverlayEffect } from '@core/services/overlay-stack.service';

@Component({
  selector: 'app-drawer',
  standalone: true,
  imports: [A11yModule, PortalModule, OverlayModule],
  templateUrl: './drawer.component.html',
  styleUrl: './drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DrawerComponent {
  private readonly overlay = inject(Overlay);
  private readonly destroyRef = inject(DestroyRef);
  private previousFocus: HTMLElement | null = null;
  private overlayRef: OverlayRef | null = null;

  readonly titleId = generateControlId('drawer-title');

  title = input<string>();
  visible = model(false);
  closeOnBackdrop = input(true);

  closed = output<void>();

  private readonly portal = viewChild.required(CdkPortal);
  private readonly isTopmostOverlay = registerOverlayEffect(this.visible);

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.attach();
      } else {
        this.detach();
      }
    });
    this.destroyRef.onDestroy(() => this.detach());
  }

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

  private attach(): void {
    if (this.overlayRef) {
      return;
    }
    this.previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    this.overlayRef = this.overlay.create({
      hasBackdrop: true,
      backdropClass: 'drawer-backdrop',
      scrollStrategy: this.overlay.scrollStrategies.block(),
      positionStrategy: this.overlay.position().global().right().top('0'),
      height: '100%',
    });
    this.overlayRef.backdropClick().subscribe(() => {
      if (this.closeOnBackdrop()) {
        this.close();
      }
    });
    this.overlayRef.attach(this.portal());
    queueMicrotask(() => this.focusFirstControl());
  }

  private detach(): void {
    if (!this.overlayRef) {
      return;
    }
    this.overlayRef.dispose();
    this.overlayRef = null;
    this.restoreFocus();
  }

  private focusFirstControl(): void {
    const panel = this.overlayRef?.overlayElement.querySelector('.drawer__body') as HTMLElement | null;
    const focusable = panel?.querySelector(
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
