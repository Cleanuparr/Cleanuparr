import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { ConfirmService } from '@core/services/confirm.service';
import { HasPendingChanges, pendingChangesGuard } from './pending-changes.guard';

describe('pendingChangesGuard', () => {
  function setup(dirty: boolean) {
    const confirmService = TestBed.inject(ConfirmService);
    const component: HasPendingChanges = { hasPendingChanges: () => dirty };
    const route = {} as ActivatedRouteSnapshot;
    const state = {} as RouterStateSnapshot;

    const run = () =>
      TestBed.runInInjectionContext(() => pendingChangesGuard(component, route, state, state));

    return { confirmService, run };
  }

  it('allows navigation without prompting when there are no pending changes', () => {
    const { confirmService, run } = setup(false);

    expect(run()).toBe(true);
    expect(confirmService.state()).toBeNull();
  });

  it('opens a destructive leave-page confirmation when there are pending changes', () => {
    const { confirmService, run } = setup(true);

    const result = run();

    expect(result).toBeInstanceOf(Promise);
    expect(confirmService.state()).toMatchObject({
      title: 'Unsaved Changes',
      message: 'You have unsaved changes. Are you sure you want to leave this page?',
      confirmLabel: 'Leave',
      cancelLabel: 'Stay',
      destructive: true,
    });
  });

  it('resolves to true and clears the confirmation when the user accepts', async () => {
    const { confirmService, run } = setup(true);

    const result = run();
    confirmService.accept();

    await expect(result).resolves.toBe(true);
    expect(confirmService.state()).toBeNull();
  });

  it('resolves to false and clears the confirmation when the user cancels', async () => {
    const { confirmService, run } = setup(true);

    const result = run();
    confirmService.cancel();

    await expect(result).resolves.toBe(false);
    expect(confirmService.state()).toBeNull();
  });
});
