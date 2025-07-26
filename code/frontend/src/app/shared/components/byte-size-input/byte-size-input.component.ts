import { Component, Input, forwardRef, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR, ReactiveFormsModule } from '@angular/forms';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectButtonModule } from 'primeng/selectbutton';

export type ByteSizeInputType = 'speed' | 'size';

type ByteSizeUnit = 'KB' | 'MB' | 'GB' | 'TB';

@Component({
  selector: 'app-byte-size-input',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, InputNumberModule, SelectButtonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => ByteSizeInputComponent),
      multi: true
    }
  ],
  templateUrl: './byte-size-input.component.html',
  styleUrl: './byte-size-input.component.scss'
})
export class ByteSizeInputComponent implements ControlValueAccessor {
  @Input() label: string = 'Size';
  @Input() min: number = 0;
  @Input() disabled: boolean = false;
  @Input() placeholder: string = 'Enter size';
  @Input() helpText: string = '';
  @Input() type: ByteSizeInputType = 'size'; // New input to control units

  // Value in the selected unit
  value = signal<number | null>(null);

  // The selected unit
  unit = signal<ByteSizeUnit>(this.getDefaultUnit());

  // Available units, computed based on type
  get unitOptions() {
    switch (this.type) {
      case 'speed':
        return [
          { label: 'KB/s', value: 'KB' },
          { label: 'MB/s', value: 'MB' }
        ];
      case 'size':
      default:
        return [
          { label: 'MB', value: 'MB' },
          { label: 'GB', value: 'GB' }
        ];
    }
  }

  // Get default unit based on type
  private getDefaultUnit(): ByteSizeUnit {
    switch (this.type) {
      case 'speed':
        return 'KB';
      case 'size':
      default:
        return 'MB';
    }
  }

  // ControlValueAccessor interface methods
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnInit(): void {
    // Set default unit based on type if not already set
    if (!this.unit()) {
      this.unit.set(this.getDefaultUnit());
    }
  }

  /**
   * Parse the string value in format '100MB', '1.5GB', etc.
   */
  writeValue(value: string): void {
    if (!value) {
      this.value.set(null);
      this.unit.set(this.getDefaultUnit());
      return;
    }

    try {
      // Parse values like "100MB", "1.5GB", etc.
      const regex = /^([\d.]+)([KMGT]B)$/i;
      const match = value.match(regex);

      if (match) {
        const numValue = parseFloat(match[1]);
        const unit = match[2].toUpperCase() as ByteSizeUnit;
        // Validate unit is allowed for this type
        const allowedUnits = this.unitOptions.map(opt => opt.value);
        if (allowedUnits.includes(unit)) {
          this.value.set(numValue);
          this.unit.set(unit);
        } else {
          // If unit not allowed, use default
          this.value.set(numValue);
          this.unit.set(this.getDefaultUnit());
        }
      } else {
        this.value.set(null);
        this.unit.set(this.getDefaultUnit());
      }
    } catch (e) {
      console.error('Error parsing byte size value:', value, e);
      this.value.set(null);
      this.unit.set(this.getDefaultUnit());
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  /**
   * Update the value and notify the form control
   */
  updateValue(): void {
    this.onTouched();
    if (this.value() === null) {
      this.onChange('');
      return;
    }
    // Format as "100MB", "1.5GB", etc.
    const formattedValue = `${this.value()}${this.unit()}`;
    this.onChange(formattedValue);
  }

  /**
   * Update the unit and notify the form control
   */
  updateUnit(): void {
    this.updateValue();
  }
}
