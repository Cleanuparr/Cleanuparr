import { Component, ChangeDetectionStrategy, input, model, computed, inject } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';
import { NewBadgeComponent } from '@ui/new-badge/new-badge.component';

@Component({
  selector: 'app-toggle',
  standalone: true,
  imports: [NgIcon, NewBadgeComponent],
  templateUrl: './toggle.component.html',
  styleUrl: './toggle.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToggleComponent {
  private readonly docs = inject(DocumentationService);

  private static nextId = 0;
  protected readonly controlId = `app-toggle-${ToggleComponent.nextId++}`;

  label = input<string>();
  featureId = input<string>();
  disabled = input(false);
  forceDisabled = input(false);
  hint = input<string>();
  helpKey = input<string>();
  beforeChange = input<(newValue: boolean) => Promise<boolean> | boolean>();
  checked = model(false);

  readonly effectiveDisabled = computed(() => this.disabled() || this.forceDisabled());

  private pending = false;

  async toggle(): Promise<void> {
    if (this.effectiveDisabled() || this.pending) return;

    const newValue = !this.checked();
    const guard = this.beforeChange();

    if (guard) {
      this.pending = true;
      try {
        const allowed = await guard(newValue);
        if (!allowed) return;
      } catch {
        return;
      } finally {
        this.pending = false;
      }
    }

    this.checked.set(newValue);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault();
      this.toggle();
    }
  }

  onHelpClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const key = this.helpKey();
    if (key) {
      const [section, field] = key.split(':');
      this.docs.openFieldDocumentation(section, field);
    }
  }
}
