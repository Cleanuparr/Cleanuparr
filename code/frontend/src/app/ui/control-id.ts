let counter = 0;

/** Unique, stable DOM id for a form control, e.g. `generateControlId('app-input')`. */
export function generateControlId(prefix: string): string {
  return `${prefix}-${++counter}`;
}
