import {
  Component,
  ChangeDetectionStrategy,
  input,
  signal,
  effect,
  OnDestroy,
  PLATFORM_ID,
  inject,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

@Component({
  selector: 'app-animated-counter',
  standalone: true,
  template: `{{ displayValue() }}`,
  styles: `
    :host {
      display: inline;
      font-variant-numeric: tabular-nums;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnimatedCounterComponent implements OnDestroy {
  /** The target value to animate to */
  value = input.required<number>();

  /** Duration in ms (defaults to 600ms) */
  duration = input(600);

  displayValue = signal(0);

  private animationId = 0;
  private readonly platformId = inject(PLATFORM_ID);

  constructor() {
    effect(() => {
      const target = this.value();
      const dur = this.duration();

      if (!isPlatformBrowser(this.platformId)) {
        this.displayValue.set(target);
        return;
      }

      // Skip animation in performance mode
      if (document.documentElement.dataset['performanceMode'] === 'true') {
        this.displayValue.set(target);
        return;
      }

      this.animateTo(target, dur);
    });
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.animationId);
  }

  private animateTo(target: number, duration: number): void {
    cancelAnimationFrame(this.animationId);

    const start = this.displayValue();
    const diff = target - start;
    if (diff === 0) return;

    const startTime = performance.now();

    const step = (now: number) => {
      const elapsed = now - startTime;
      const progress = Math.min(elapsed / duration, 1);
      // Ease-out cubic for a smooth deceleration
      const eased = 1 - Math.pow(1 - progress, 3);

      this.displayValue.set(Math.round(start + diff * eased));

      if (progress < 1) {
        this.animationId = requestAnimationFrame(step);
      }
    };

    this.animationId = requestAnimationFrame(step);
  }
}
