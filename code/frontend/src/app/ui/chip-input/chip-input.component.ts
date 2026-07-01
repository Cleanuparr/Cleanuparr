import { Component, ChangeDetectionStrategy, input, model, signal, computed, effect, untracked, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';
import { NewBadgeComponent } from '@ui/new-badge/new-badge.component';

function sameItems(a: string[], b: string[]): boolean {
  return a.length === b.length && a.every((v, i) => v === b[i]);
}

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

  private static nextId = 0;
  protected readonly controlId = `app-chip-${ChipInputComponent.nextId++}`;

  label = input<string>();
  featureId = input<string>();
  placeholder = input('Type and press Enter...');
  disabled = input(false);
  forceDisabled = input(false);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  items = model<string[]>([]);
  // `value` mirrors `items` so the control also satisfies Signal Forms' FormValueControl
  // contract and can be bound via [formField]. Legacy [(items)] call sites keep working.
  value = model<string[]>([]);

  readonly inputValue = signal('');
  readonly touched = signal(false);

  readonly effectiveDisabled = computed(() => this.disabled() || this.forceDisabled());

  constructor() {
    // `value` ([formField]) and `items` ([(items)] call sites) are both two-way models mirroring the same list. Guard each write on shallow-equality so the
    // pair can never ping-pong into an infinite loop, even if a future edit copies or maps the array instead of passing the same reference through.
    effect(() => {
      const v = this.value();
      untracked(() => {
        if (!sameItems(this.items(), v)) {
          this.items.set(v);
        }
      });
    });
    effect(() => {
      const items = this.items();
      untracked(() => {
        if (!sameItems(this.value(), items)) {
          this.value.set(items);
        }
      });
    });
  }

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
