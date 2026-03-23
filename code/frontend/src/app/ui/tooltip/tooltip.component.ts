import { Component, ChangeDetectionStrategy, input, signal, ElementRef, viewChild } from '@angular/core';

type TooltipPosition = 'top' | 'bottom' | 'left' | 'right';

@Component({
  selector: 'app-tooltip',
  standalone: true,
  templateUrl: './tooltip.component.html',
  styleUrl: './tooltip.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(mouseenter)': 'onMouseEnter()',
    '(mouseleave)': 'onMouseLeave()',
  },
})
export class TooltipComponent {
  text = input.required<string>();
  position = input<TooltipPosition>('top');

  resolvedPosition = signal<TooltipPosition>('top');
  visible = signal(false);

  private wrapper = viewChild<ElementRef<HTMLElement>>('wrapper');

  onMouseEnter(): void {
    this.resolvedPosition.set(this.calculatePosition());
    this.visible.set(true);
  }

  onMouseLeave(): void {
    this.visible.set(false);
  }

  private calculatePosition(): TooltipPosition {
    const preferred = this.position();
    const wrapperEl = this.wrapper()?.nativeElement;
    if (!wrapperEl) return preferred;

    const rect = wrapperEl.getBoundingClientRect();
    const margin = 12;
    const estimatedTooltipHeight = 50;
    const estimatedTooltipWidth = 230;

    // Find the actual content boundary (accounts for sidebar)
    const contentArea = wrapperEl.closest('.shell__content') as HTMLElement | null;
    const contentLeft = contentArea ? contentArea.getBoundingClientRect().left : 0;

    const spaceTop = rect.top;
    const spaceBottom = window.innerHeight - rect.bottom;
    const spaceLeft = rect.left - contentLeft;
    const spaceRight = window.innerWidth - rect.right;

    const hasSpace: Record<TooltipPosition, boolean> = {
      top: spaceTop > estimatedTooltipHeight + margin,
      bottom: spaceBottom > estimatedTooltipHeight + margin,
      left: spaceLeft > estimatedTooltipWidth + margin,
      right: spaceRight > estimatedTooltipWidth + margin,
    };

    if (hasSpace[preferred] && (preferred === 'left' || preferred === 'right')) {
      return preferred;
    }

    if (hasSpace[preferred] && (preferred === 'top' || preferred === 'bottom')) {
      const halfTooltip = estimatedTooltipWidth / 2;
      const centerX = rect.left + rect.width / 2;

      // Check if centered tooltip would overflow left (behind sidebar)
      if (centerX - halfTooltip < contentLeft + margin) {
        if (hasSpace['right']) return 'right';
        if (hasSpace['bottom']) return 'bottom';
      }

      // Check if centered tooltip would overflow right
      if (centerX + halfTooltip > window.innerWidth - margin) {
        if (hasSpace['left']) return 'left';
        if (hasSpace['bottom']) return 'bottom';
      }

      return preferred;
    }

    // Fallback order
    const fallbacks: TooltipPosition[] =
      preferred === 'top' || preferred === 'bottom'
        ? ['top', 'bottom', 'right', 'left']
        : ['right', 'left', 'top', 'bottom'];

    for (const pos of fallbacks) {
      if (hasSpace[pos]) return pos;
    }

    return preferred;
  }
}
