import { Signal, computed } from '@angular/core';

/** Combines a control's own `disabled` with an external `forceDisabled` override. */
export function effectiveDisabled(disabled: Signal<boolean>, forceDisabled: Signal<boolean>): Signal<boolean> {
  return computed(() => disabled() || forceDisabled());
}
