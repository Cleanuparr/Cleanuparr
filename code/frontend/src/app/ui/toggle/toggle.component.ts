import { Component, ChangeDetectionStrategy, input, model, inject } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';

@Component({
  selector: 'app-toggle',
  standalone: true,
  imports: [NgIcon],
  templateUrl: './toggle.component.html',
  styleUrl: './toggle.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToggleComponent {
  private readonly docs = inject(DocumentationService);

  label = input<string>();
  disabled = input(false);
  hint = input<string>();
  helpKey = input<string>();
  beforeChange = input<(newValue: boolean) => Promise<boolean> | boolean>();
  checked = model(false);

  private pending = false;

  async toggle(): Promise<void> {
    if (this.disabled() || this.pending) return;

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
