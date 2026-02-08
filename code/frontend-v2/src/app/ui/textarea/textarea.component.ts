import { Component, ChangeDetectionStrategy, input, model, output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';

@Component({
  selector: 'app-textarea',
  standalone: true,
  imports: [FormsModule, NgIcon],
  templateUrl: './textarea.component.html',
  styleUrl: './textarea.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TextareaComponent {
  private readonly docs = inject(DocumentationService);

  label = input<string>();
  placeholder = input('');
  disabled = input(false);
  readonly = input(false);
  rows = input(4);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  value = model('');

  blurred = output<FocusEvent>();

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
