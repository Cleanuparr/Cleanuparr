import { Directive, HostListener } from '@angular/core';
import { NgControl } from '@angular/forms';

/**
 * Directive that restricts input to numeric characters with an optional leading minus sign.
 * Useful for Telegram chat IDs which can be negative for groups/supergroups.
 */
@Directive({
  selector: '[signedNumericInput]',
  standalone: true
})
export class SignedNumericInputDirective {
  constructor(private ngControl: NgControl) {}

  @HostListener('input', ['$event'])
  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const originalValue = input.value;
    const sanitized = this.sanitize(originalValue);

    if (sanitized !== originalValue) {
      input.value = sanitized;
      this.ngControl.control?.setValue(sanitized);
    }
  }

  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    // Allow: backspace, delete, tab, escape, enter
    if ([8, 9, 27, 13, 46].includes(event.keyCode) ||
        // Allow: Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
        (event.keyCode === 65 && event.ctrlKey) ||
        (event.keyCode === 67 && event.ctrlKey) ||
        (event.keyCode === 86 && event.ctrlKey) ||
        (event.keyCode === 88 && event.ctrlKey) ||
        // Allow: home, end, left, right
        (event.keyCode >= 35 && event.keyCode <= 39)) {
      return;
    }

    // Allow minus only at the start and only if not already present
    if ((event.key === '-' || event.keyCode === 189 || event.keyCode === 109)) {
      const input = event.target as HTMLInputElement;
      const hasMinus = input.value.includes('-');
      const cursorAtStart = (input.selectionStart ?? 0) === 0;
      if (!hasMinus && cursorAtStart) {
        return;
      }
      event.preventDefault();
      return;
    }

    // Block non-numeric keys
    if ((event.shiftKey || (event.keyCode < 48 || event.keyCode > 57)) && (event.keyCode < 96 || event.keyCode > 105)) {
      event.preventDefault();
    }
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    const pasted = event.clipboardData?.getData('text') || '';
    const sanitized = this.sanitize(pasted);

    if (sanitized !== pasted) {
      event.preventDefault();
      const input = event.target as HTMLInputElement;
      const currentValue = input.value;
      const start = input.selectionStart ?? 0;
      const end = input.selectionEnd ?? 0;

      const newValue = currentValue.substring(0, start) + sanitized + currentValue.substring(end);
      input.value = newValue;
      this.ngControl.control?.setValue(newValue);

      const cursor = start + sanitized.length;
      setTimeout(() => input.setSelectionRange(cursor, cursor));
    }
  }

  private sanitize(value: string): string {
    if (!value) return '';

    const hasMinus = value.startsWith('-');
    const digits = value.replace(/\D/g, '');
    return hasMinus ? `-${digits}` : digits;
  }
}
