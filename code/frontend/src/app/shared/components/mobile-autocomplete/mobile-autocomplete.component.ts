import { Component, Input, forwardRef, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, NG_VALIDATORS, FormsModule, AbstractControl, ValidationErrors, Validator } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';

@Component({
  selector: 'app-mobile-autocomplete',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    InputTextModule,
    ButtonModule,
    ChipModule
  ],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MobileAutocompleteComponent),
      multi: true
    },
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => MobileAutocompleteComponent),
      multi: true
    }
  ],
  templateUrl: './mobile-autocomplete.component.html',
  styleUrls: ['./mobile-autocomplete.component.scss']
})
export class MobileAutocompleteComponent implements ControlValueAccessor, Validator {
  @Input() placeholder: string = 'Add item and press Enter';
  @Input() multiple: boolean = true;
  @ViewChild('inputField', { static: false }) inputField?: ElementRef<HTMLInputElement>;

  value: string[] = [];
  disabled: boolean = false;
  currentInputValue: string = '';
  hasUncommittedInput: boolean = false;
  touched: boolean = false;

  private onChange = (value: string[]) => {};
  private onTouched = () => {};
  private onValidatorChange = () => {};

  onInputChange(value: string): void {
    this.currentInputValue = value;
    this.hasUncommittedInput = value.trim().length > 0;
    this.onValidatorChange();
  }

  onInputBlur(): void {
    this.touched = true;
    this.onTouched();
    this.onValidatorChange();
  }

  writeValue(value: string[]): void {
    this.value = value || [];
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;

    // Clear uncommitted input when becoming disabled
    if (isDisabled && this.hasUncommittedInput) {
      this.currentInputValue = '';
      this.hasUncommittedInput = false;
      this.onValidatorChange();
    }
  }

  validate(control: AbstractControl): ValidationErrors | null {
    // Don't report validation errors when disabled
    if (this.hasUncommittedInput && !this.disabled) {
      return { uncommittedInput: { value: this.currentInputValue } };
    }
    return null;
  }

  registerOnValidatorChange(fn: () => void): void {
    this.onValidatorChange = fn;
  }

  addItem(item: string): void {
    if (item && item.trim() && !this.disabled) {
      const trimmedItem = item.trim();

      if (!this.value.includes(trimmedItem)) {
        const newValue = [...this.value, trimmedItem];
        this.value = newValue;
        this.onChange(this.value);
      }

      this.currentInputValue = '';
      this.hasUncommittedInput = false;
      this.onValidatorChange();
    }
  }

  addItemAndClearInput(inputField: HTMLInputElement): void {
    this.addItem(inputField.value);
    inputField.value = '';
    this.onInputChange('');
  }

  removeItem(index: number): void {
    if (!this.disabled) {
      const newValue = this.value.filter((_, i) => i !== index);
      this.value = newValue;
      this.onChange(this.value);
      this.onTouched();
    }
  }
}
