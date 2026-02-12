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
  checked = model(false);

  toggle(): void {
    if (!this.disabled()) {
      this.checked.set(!this.checked());
    }
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
