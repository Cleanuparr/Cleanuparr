import { Component, Input, Output, EventEmitter, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
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
    }
  ],
  template: `
    <div class="mobile-autocomplete-container">
      <div class="input-with-button">
        <input 
          type="text" 
          pInputText 
          #inputField
          [placeholder]="placeholder"
          (keyup.enter)="addItem(inputField.value); inputField.value = ''"
          class="mobile-input"
        />
        <button 
          pButton 
          type="button" 
          icon="pi pi-plus" 
          class="p-button-sm add-button"
          (click)="addItem(inputField.value); inputField.value = ''"
          [title]="'Add ' + placeholder"
        ></button>
      </div>
      <div class="chips-container" *ngIf="value && value.length > 0">
        <p-chip 
          *ngFor="let item of value; let i = index" 
          [label]="item" 
          [removable]="true"
          (onRemove)="removeItem(i)"
          class="mb-2 mr-2"
        ></p-chip>
      </div>
    </div>
  `,
  styleUrls: ['./mobile-autocomplete.component.scss']
})
export class MobileAutocompleteComponent implements ControlValueAccessor {
  @Input() placeholder: string = 'Add item and press Enter';
  @Input() multiple: boolean = true;
  
  value: string[] = [];
  disabled: boolean = false;

  // ControlValueAccessor implementation
  private onChange = (value: string[]) => {};
  private onTouched = () => {};

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
  }

  addItem(item: string): void {
    if (item && item.trim() && !this.disabled) {
      const trimmedItem = item.trim();
      
      // Check if item already exists
      if (!this.value.includes(trimmedItem)) {
        const newValue = [...this.value, trimmedItem];
        this.value = newValue;
        this.onChange(this.value);
        this.onTouched();
      }
    }
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