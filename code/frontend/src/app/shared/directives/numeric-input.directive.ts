import { Directive, HostListener } from '@angular/core';
import { NgControl } from '@angular/forms';

/**
 * Directive that restricts input to numeric characters only.
 * Useful for fields that need to accept very long numeric values like Discord channel IDs
 * that exceed JavaScript's safe integer limits.
 * 
 * Usage: <input type="text" numericInput formControlName="channelId" />
 */
@Directive({
  selector: '[numericInput]',
  standalone: true
})
export class NumericInputDirective {
  private regex = /^\d*$/; // Only allow positive integers (no decimals or negative numbers)

  constructor(private ngControl: NgControl) {}

  @HostListener('input', ['$event'])
  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const originalValue = input.value;

    if (!this.regex.test(originalValue)) {
      // Strip all non-numeric characters
      const sanitized = originalValue.replace(/[^\d]/g, '');
      
      // Update the form control value
      this.ngControl.control?.setValue(sanitized);
      
      // Update the input display value
      input.value = sanitized;
    }
  }

  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    // Allow: backspace, delete, tab, escape, enter
    if ([8, 9, 27, 13, 46].indexOf(event.keyCode) !== -1 ||
        // Allow: Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
        (event.keyCode === 65 && event.ctrlKey === true) ||
        (event.keyCode === 67 && event.ctrlKey === true) ||
        (event.keyCode === 86 && event.ctrlKey === true) ||
        (event.keyCode === 88 && event.ctrlKey === true) ||
        // Allow: home, end, left, right
        (event.keyCode >= 35 && event.keyCode <= 39)) {
      return;
    }
    
    // Ensure that it is a number and stop the keypress
    if ((event.shiftKey || (event.keyCode < 48 || event.keyCode > 57)) && (event.keyCode < 96 || event.keyCode > 105)) {
      event.preventDefault();
    }
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    const paste = event.clipboardData?.getData('text') || '';
    const sanitized = paste.replace(/[^\d]/g, '');
    
    // If the paste content has non-numeric characters, prevent default and handle manually
    if (sanitized !== paste) {
      event.preventDefault();
      
      const input = event.target as HTMLInputElement;
      const currentValue = input.value;
      const start = input.selectionStart || 0;
      const end = input.selectionEnd || 0;
      
      const newValue = currentValue.substring(0, start) + sanitized + currentValue.substring(end);
      
      // Update both the input value and form control
      input.value = newValue;
      this.ngControl.control?.setValue(newValue);
      
      // Set cursor position after pasted content
      setTimeout(() => {
        input.setSelectionRange(start + sanitized.length, start + sanitized.length);
      });
    }
    // If paste content is all numeric, allow normal paste behavior
    // The input event will handle form control synchronization
  }
}
