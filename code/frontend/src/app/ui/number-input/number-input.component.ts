import { Component, ChangeDetectionStrategy, input, model, output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';
import { NewBadgeComponent } from '@ui/new-badge/new-badge.component';
import { generateControlId } from '@ui/control-id';

@Component({
  selector: 'app-number-input',
  standalone: true,
  imports: [FormsModule, NgIcon, NewBadgeComponent],
  templateUrl: './number-input.component.html',
  styleUrl: './number-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NumberInputComponent {
  private readonly docs = inject(DocumentationService);

  protected readonly controlId = generateControlId('app-number');

  label = input<string>();
  featureId = input<string>();
  placeholder = input('');
  disabled = input(false);
  min = input<number>();
  max = input<number>();
  step = input(1);
  /** Sentinel below the usable range (e.g. -1 for "disabled"); values between it and 0 are skipped. */
  offValue = input<number>();
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
      if (minVal != null) {
        clamped = Math.max(clamped, minVal);
      }
      if (maxVal != null) {
        clamped = Math.min(clamped, maxVal);
      }
      clamped = this.snapOutOfGap(clamped);
      if (clamped !== current) {
        this.value.set(clamped);
      }
    }
    this.blurred.emit(event);
  }

  /** Pulls a value sitting in the unusable gap onto whichever edge is closer. */
  private snapOutOfGap(value: number): number {
    const off = this.offValue();
    if (off == null || value <= off || value >= 0) {
      return value;
    }
    return value <= off / 2 ? off : 0;
  }

  private stepDecimals(): number {
    let n = this.step();
    let decimals = 0;
    while (Math.round(n) !== n && decimals < 10) {
      n *= 10;
      decimals++;
    }
    return decimals;
  }

  increment(): void { this.applyStep(1); }
  decrement(): void { this.applyStep(-1); }

  private applyStep(direction: 1 | -1): void {
    if (this.disabled()) return;
    const current = this.value() ?? 0;
    const off = this.offValue();
    if (off != null && direction === 1 && current === off) {
      this.value.set(0);
      return;
    }
    const next = parseFloat((current + direction * this.step()).toFixed(this.stepDecimals()));
    const minVal = this.min();
    const maxVal = this.max();
    let clamped = next;
    if (minVal != null) {
      clamped = Math.max(clamped, minVal);
    }
    if (maxVal != null) {
      clamped = Math.min(clamped, maxVal);
    }
    if (off != null && clamped > off && clamped < 0) {
      clamped = off;
    }
    this.value.set(clamped);
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
