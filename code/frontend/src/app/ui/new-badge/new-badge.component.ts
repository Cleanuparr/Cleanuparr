import { Component, ChangeDetectionStrategy, input, computed, inject } from '@angular/core';
import { FeatureBadgeService } from '@core/feature-badges/feature-badge.service';

export type NewBadgeVariant = 'ribbon' | 'inline';

/**
 * A "NEW" indicator for recently-shipped features.
 *
 * - `ribbon` (default): a flame-colored corner ribbon pinned to the top-right corner of its
 *   nearest positioned ancestor (e.g. an `app-card` or `app-accordion`). The host uses
 *   `display: contents` so the absolutely positioned ribbon anchors to that container.
 * - `inline`: a small flame-colored pill that flows inline next to a label, for field-level
 *   elements (toggles, inputs, selects, …) where a corner ribbon does not fit.
 */
@Component({
  selector: 'app-new-badge',
  standalone: true,
  template: `
    @if (show()) {
      @if (variant() === 'inline') {
        <span class="new-pill" aria-label="New feature">NEW</span>
      } @else {
        <span class="new-ribbon-wrap" aria-label="New feature">
          <span class="new-ribbon">NEW</span>
        </span>
      }
    }
  `,
  styleUrl: './new-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewBadgeComponent {
  private readonly featureBadge = inject(FeatureBadgeService);

  featureId = input.required<string>();
  variant = input<NewBadgeVariant>('ribbon');

  protected readonly show = computed(() => this.featureBadge.isNew(this.featureId()));
}
