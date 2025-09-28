import { AbstractControl, ValidationErrors } from '@angular/forms';

export class UrlValidators {
  /**
   * Generic http/https URL validator used by the various settings components.
   * Returns { invalidUri: true } when URL parsing fails or { invalidProtocol: true }
   * when protocol is not http/https. Returns null for valid values or empty.
   */
  public static httpUrl(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) {
      return null;
    }

    try {
      const url = new URL(value);
      if (url.protocol !== 'http:' && url.protocol !== 'https:') {
        return { invalidProtocol: true };
      }
      return null;
    } catch {
      return { invalidUri: true };
    }
  }
}
