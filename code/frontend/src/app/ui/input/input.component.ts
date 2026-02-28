import { Component, ChangeDetectionStrategy, input, model, output, ElementRef, viewChild, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';

@Component({
  selector: 'app-input',
  standalone: true,
  imports: [FormsModule, NgIcon],
  templateUrl: './input.component.html',
  styleUrl: './input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InputComponent {
  private readonly docs = inject(DocumentationService);

  label = input<string>();
  placeholder = input('');
  type = input<'text' | 'password' | 'email' | 'url' | 'search' | 'datetime-local' | 'date' | 'number'>('text');
  disabled = input(false);
  readonly = input(false);
  revealable = input(true);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  value = model('');

  inputRef = viewChild<ElementRef<HTMLInputElement>>('inputEl');

  blurred = output<FocusEvent>();

  readonly showSecret = signal(false);
  readonly hasEye = computed(() => this.type() === 'password' && this.revealable());
  readonly effectiveType = computed(() => {
    if (this.hasEye() && this.showSecret()) return 'text';
    return this.type();
  });

  focus(): void {
    this.inputRef()?.nativeElement.focus();
  }

  toggleSecret(event: Event): void {
    event.preventDefault();
    if (!this.revealable()) return;
    this.showSecret.update(v => !v);
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
