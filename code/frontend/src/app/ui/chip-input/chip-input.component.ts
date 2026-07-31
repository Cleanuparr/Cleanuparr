import { Component, ChangeDetectionStrategy, input, model, signal, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';
import { NewBadgeComponent } from '@ui/new-badge/new-badge.component';
import { generateControlId } from '@ui/control-id';
import { effectiveDisabled as computeEffectiveDisabled } from '@ui/effective-disabled';

@Component({
  selector: 'app-chip-input',
  standalone: true,
  imports: [FormsModule, NgIcon, NewBadgeComponent],
  templateUrl: './chip-input.component.html',
  styleUrl: './chip-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChipInputComponent {
  private readonly docs = inject(DocumentationService);

  protected readonly controlId = generateControlId('app-chip');

  label = input<string>();
  featureId = input<string>();
  placeholder = input('Type and press Enter...');
  disabled = input(false);
  forceDisabled = input(false);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  value = model<string[]>([]);

  readonly inputValue = signal('');
  readonly touched = signal(false);

  readonly effectiveDisabled = computeEffectiveDisabled(this.disabled, this.forceDisabled);

  readonly hasUncommittedInput = computed(() => {
    return this.inputValue().trim().length > 0 && !this.effectiveDisabled();
  });

  readonly uncommittedError = computed(() => {
    if (this.hasUncommittedInput() && (this.touched() || this.inputValue().length > 0)) {
      return 'Press Enter or the + button to add this item';
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

  commitInput(): void {
    const val = this.inputValue().trim();
    if (val) {
      this.addItem(val);
    }
  }

  onBlur(): void {
    this.touched.set(true);
  }

  addItem(item: string): void {
    if (!this.value().includes(item)) {
      this.value.update((items) => [...items, item]);
    }
    this.inputValue.set('');
  }

  removeItem(index: number): void {
    this.value.update((items) => items.filter((_, i) => i !== index));
  }

  private removeLastItem(): void {
    if (this.value().length > 0) {
      this.value.update((items) => items.slice(0, -1));
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
