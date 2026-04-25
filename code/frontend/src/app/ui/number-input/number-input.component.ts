import { Component, ChangeDetectionStrategy, input, model, output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';

@Component({
  selector: 'app-number-input',
  standalone: true,
  imports: [FormsModule, NgIcon],
  templateUrl: './number-input.component.html',
  styleUrl: './number-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NumberInputComponent {
  private readonly docs = inject(DocumentationService);

  label = input<string>();
  placeholder = input('');
  disabled = input(false);
  min = input<number>();
  max = input<number>();
  step = input(1);
  suffix = input<string>();
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  value = model<number | null>(null);

  blurred = output<FocusEvent>();

  onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    if (target.value === '') {
      this.value.set(null);
      return;
    }
    this.value.set(Number(target.value));
  }

  onBlur(event: FocusEvent): void {
    const current = this.value();
    if (current != null) {
      let clamped = current;
      const minVal = this.min();
      const maxVal = this.max();
      if (minVal != null) { clamped = Math.max(clamped, minVal); }
      if (maxVal != null) { clamped = Math.min(clamped, maxVal); }
      if (clamped !== current) {
        this.value.set(clamped);
      }
    }
    this.blurred.emit(event);
  }

  increment(): void {
    if (this.disabled()) return;
    const current = this.value() ?? 0;
    const maxVal = this.max();
    const next = current + this.step();
    this.value.set(maxVal != null ? Math.min(next, maxVal) : next);
  }

  decrement(): void {
    if (this.disabled()) return;
    const current = this.value() ?? 0;
    const minVal = this.min();
    const next = current - this.step();
    this.value.set(minVal != null ? Math.max(next, minVal) : next);
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
