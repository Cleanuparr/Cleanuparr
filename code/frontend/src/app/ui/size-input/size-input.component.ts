import { Component, ChangeDetectionStrategy, input, model, signal, effect, untracked, inject } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';

export interface SizeUnit {
  label: string;
  value: string;
}

@Component({
  selector: 'app-size-input',
  standalone: true,
  imports: [NgIcon],
  templateUrl: './size-input.component.html',
  styleUrl: './size-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SizeInputComponent {
  private readonly docs = inject(DocumentationService);

  label = input<string>();
  units = input.required<SizeUnit[]>();
  placeholder = input('');
  disabled = input(false);
  min = input<number>(0);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  value = model('');

  readonly numericValue = signal<number | null>(null);
  readonly selectedUnit = signal('');

  private syncing = false;

  constructor() {
    // Parse incoming value string into numeric + unit
    effect(() => {
      const val = this.value();
      const units = this.units();
      if (this.syncing || units.length === 0) return;

      untracked(() => {
        const parsed = this.parseValue(val, units);
        this.numericValue.set(parsed.numeric);
        this.selectedUnit.set(parsed.unit);
      });
    });
  }

  private parseValue(val: string, units: SizeUnit[]): { numeric: number | null; unit: string } {
    const defaultUnit = units[0]?.value ?? '';

    if (!val || !val.trim()) {
      return { numeric: null, unit: this.selectedUnit() || defaultUnit };
    }

    const trimmed = val.trim().toUpperCase();

    // Try matching each unit suffix (longest first to avoid partial matches)
    const sortedUnits = [...units].sort((a, b) => b.value.length - a.value.length);
    for (const u of sortedUnits) {
      if (trimmed.endsWith(u.value.toUpperCase())) {
        const numStr = trimmed.slice(0, -u.value.length).trim();
        const num = numStr ? Number(numStr) : null;
        if (num !== null && !isNaN(num)) {
          return { numeric: num, unit: u.value };
        }
      }
    }

    // Try parsing as plain number, keep current unit
    const num = Number(trimmed);
    if (!isNaN(num)) {
      return { numeric: num, unit: this.selectedUnit() || defaultUnit };
    }

    return { numeric: null, unit: this.selectedUnit() || defaultUnit };
  }

  onNumericInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    const num = target.value === '' ? null : Number(target.value);
    this.numericValue.set(num);
    this.compose();
  }

  selectUnit(unitValue: string): void {
    if (this.disabled()) return;
    this.selectedUnit.set(unitValue);
    this.compose();
  }

  private compose(): void {
    const num = this.numericValue();
    const unit = this.selectedUnit();
    this.syncing = true;
    if (num == null || unit === '') {
      this.value.set('');
    } else {
      this.value.set(`${num}${unit}`);
    }
    this.syncing = false;
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
