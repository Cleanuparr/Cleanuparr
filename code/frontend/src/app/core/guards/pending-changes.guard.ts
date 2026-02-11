import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { ConfirmService } from '@core/services/confirm.service';

export interface HasPendingChanges {
  hasPendingChanges(): boolean;
}

export const pendingChangesGuard: CanDeactivateFn<HasPendingChanges> = (component) => {
  if (!component.hasPendingChanges()) {
    return true;
  }

  const confirmService = inject(ConfirmService);
  return confirmService.confirm({
    title: 'Unsaved Changes',
    message: 'You have unsaved changes. Are you sure you want to leave this page?',
    confirmLabel: 'Leave',
    cancelLabel: 'Stay',
    destructive: true,
  });
};
