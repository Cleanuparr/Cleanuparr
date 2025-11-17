import { AbstractControl, FormGroup, FormArray } from '@angular/forms';

/**
 * Utility functions for form validation
 */

/**
 * Recursively checks if a form control or form group has any validation errors
 * @param control The form control, form group, or form array to check
 * @returns True if there are any errors in the control or its children
 */
export function hasFormErrors(control: AbstractControl | null): boolean {
  if (!control) {
    return false;
  }

  // Check if the control itself has errors
  if (control.errors && Object.keys(control.errors).length > 0) {
    return true;
  }

  // If it's a FormGroup, check all its controls
  if (control instanceof FormGroup) {
    const controls = control.controls;
    for (const key in controls) {
      if (controls.hasOwnProperty(key)) {
        if (hasFormErrors(controls[key])) {
          return true;
        }
      }
    }
  }

  // If it's a FormArray, check all its controls
  if (control instanceof FormArray) {
    for (let i = 0; i < control.length; i++) {
      if (hasFormErrors(control.at(i))) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Checks if a form control or any of its children have been touched
 * @param control The form control, form group, or form array to check
 * @returns True if the control or any of its children have been touched
 */
export function isFormTouched(control: AbstractControl | null): boolean {
  if (!control) {
    return false;
  }

  // Check if the control itself has been touched
  if (control.touched) {
    return true;
  }

  // If it's a FormGroup, check all its controls
  if (control instanceof FormGroup) {
    const controls = control.controls;
    for (const key in controls) {
      if (controls.hasOwnProperty(key)) {
        if (isFormTouched(controls[key])) {
          return true;
        }
      }
    }
  }

  // If it's a FormArray, check all its controls
  if (control instanceof FormArray) {
    for (let i = 0; i < control.length; i++) {
      if (isFormTouched(control.at(i))) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Checks if a form section has validation errors and has been touched
 * This is useful for showing validation errors only after user interaction
 * @param control The form control or group to check
 * @returns True if there are errors and the control has been touched
 */
export function hasTouchedFormErrors(control: AbstractControl | null): boolean {
  return hasFormErrors(control) && isFormTouched(control);
}

/**
 * Checks if a form control or group has validation errors where each errored field has been touched
 * Only returns true if the specific invalid fields have been touched, not just any sibling
 * @param control The form control or group to check
 * @returns True if there are errors in fields that have been individually touched
 */
export function hasIndividuallyDirtyFormErrors(control: AbstractControl | null): boolean {
  if (!control) {
    return false;
  }

  // For a single control, check if it has errors AND is touched
  if (!(control instanceof FormGroup) && !(control instanceof FormArray)) {
    return control.invalid && control.dirty;
  }

  // For FormGroup, check each child recursively
  if (control instanceof FormGroup) {
    const controls = control.controls;
    for (const key in controls) {
      if (controls.hasOwnProperty(key)) {
        if (hasIndividuallyDirtyFormErrors(controls[key])) {
          return true;
        }
      }
    }
  }

  // For FormArray, check each element recursively
  if (control instanceof FormArray) {
    for (let i = 0; i < control.length; i++) {
      if (hasIndividuallyDirtyFormErrors(control.at(i))) {
        return true;
      }
    }
  }

  return false;
}
