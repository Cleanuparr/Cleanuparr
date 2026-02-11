import { Component, ChangeDetectionStrategy, input, model, signal, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';

@Component({
  selector: 'app-chip-input',
  standalone: true,
  imports: [FormsModule, NgIcon],
  templateUrl: './chip-input.component.html',
  styleUrl: './chip-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChipInputComponent {
  private readonly docs = inject(DocumentationService);

  label = input<string>();
  placeholder = input('Type and press Enter...');
  disabled = input(false);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  items = model<string[]>([]);

  readonly inputValue = signal('');
  readonly touched = signal(false);

  readonly hasUncommittedInput = computed(() => {
    return this.inputValue().trim().length > 0 && !this.disabled();
  });

  readonly uncommittedError = computed(() => {
    if (this.hasUncommittedInput() && (this.touched() || this.inputValue().length > 0)) {
      return 'Press Enter to add this item';
    }
    return undefined;
  });

  onKeydown(event: KeyboardEvent): void {
    const val = this.inputValue().trim();
    if (event.key === 'Enter' && val) {
      event.preventDefault();
      this.addItem(val);
    } else if (event.key === 'Backspace' && !this.inputValue()) {
      this.removeLastItem();
    }
  }

  onBlur(): void {
    this.touched.set(true);
  }

  addItem(value: string): void {
    if (!this.items().includes(value)) {
      this.items.update((items) => [...items, value]);
    }
    this.inputValue.set('');
  }

  removeItem(index: number): void {
    this.items.update((items) => items.filter((_, i) => i !== index));
  }

  private removeLastItem(): void {
    if (this.items().length > 0) {
      this.items.update((items) => items.slice(0, -1));
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
