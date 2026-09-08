import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { AccountApi, ChangePasswordRequest } from '@core/api/account.api';
import { ToastService } from '@core/services/toast.service';
import { ChangePasswordCardComponent } from './change-password-card.component';

describe('ChangePasswordCardComponent', () => {
  function setup(options: { fails?: boolean; pending?: boolean; oidcExclusiveMode?: boolean } = {}) {
    const toasts: string[] = [];
    const requests: ChangePasswordRequest[] = [];
    const pending = new Subject<void>();

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccountApi,
          useValue: {
            changePassword: (request: ChangePasswordRequest) => {
              requests.push(request);
              if (options.fails) {
                return throwError(() => new Error('boom'));
              }
              return options.pending ? pending : of(undefined);
            },
          },
        },
        {
          provide: ToastService,
          useValue: {
            success: (message: string) => toasts.push(`success:${message}`),
            error: (message: string) => toasts.push(`error:${message}`),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ChangePasswordCardComponent);
    fixture.componentRef.setInput('oidcExclusiveMode', options.oidcExclusiveMode ?? false);
    fixture.detectChanges();
    return { fixture, toasts, requests, pending };
  }

  function fill(
    fixture: ComponentFixture<ChangePasswordCardComponent>,
    values: { current?: string; next?: string; confirm?: string },
  ): void {
    const inputs = Array.from<HTMLInputElement>(fixture.nativeElement.querySelectorAll('input'));
    const entries: [string | undefined, HTMLInputElement][] = [
      [values.current, inputs[0]],
      [values.next, inputs[1]],
      [values.confirm, inputs[2]],
    ];
    for (const [value, element] of entries) {
      if (value !== undefined) {
        element.value = value;
        element.dispatchEvent(new Event('input'));
        element.dispatchEvent(new Event('blur'));
      }
    }
    fixture.detectChanges();
  }

  function submitButton(fixture: ComponentFixture<ChangePasswordCardComponent>): HTMLButtonElement {
    return Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button.btn')).at(-1)!;
  }

  function errors(fixture: ComponentFixture<ChangePasswordCardComponent>): string[] {
    return Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('.input-error'))
      .map(element => element.textContent!.trim());
  }

  it('keeps the submit button disabled until every field is filled', () => {
    const { fixture } = setup();

    expect(submitButton(fixture).disabled).toBe(true);

    fill(fixture, { current: 'old-secret', next: 'Str0ng-passw0rd' });

    expect(submitButton(fixture).disabled).toBe(true);

    fill(fixture, { confirm: 'Str0ng-passw0rd' });

    expect(submitButton(fixture).disabled).toBe(false);
  });

  it('reports a confirmation that does not match', () => {
    const { fixture, requests } = setup();

    fill(fixture, { current: 'old-secret', next: 'Str0ng-passw0rd', confirm: 'Str0ng-passw0rdX' });

    expect(errors(fixture)).toContain('Passwords do not match');
    expect(submitButton(fixture).disabled).toBe(true);
    expect(requests).toEqual([]);
  });

  it('reports a new password shorter than eight characters', () => {
    const { fixture, requests } = setup();

    fill(fixture, { current: 'old-secret', next: 'short7', confirm: 'short7' });

    expect(errors(fixture)).toContain('Password must be at least 8 characters');
    expect(submitButton(fixture).disabled).toBe(true);
    expect(requests).toEqual([]);
  });

  it('hides the field errors until a field has been touched', () => {
    const { fixture } = setup();

    expect(errors(fixture)).toEqual([]);
  });

  it('grades the new password and shows the strength label', () => {
    const { fixture } = setup();

    expect(fixture.componentInstance.newPasswordStrength()).toBeNull();

    fill(fixture, { next: 'short' });

    expect(fixture.componentInstance.newPasswordStrength()).toBe('weak');
    expect(
      (fixture.nativeElement.querySelector('.password-strength__label') as HTMLElement).textContent!.trim(),
    ).toBe('weak');

    fill(fixture, { next: 'longpass1' });

    expect(fixture.componentInstance.newPasswordStrength()).toBe('medium');

    fill(fixture, { next: 'L0ngPassword!' });

    expect(fixture.componentInstance.newPasswordStrength()).toBe('strong');

    fill(fixture, { next: 'aaaaaaaaaaaaaaa' });

    expect(fixture.componentInstance.newPasswordStrength()).toBe('weak');
  });

  it('shows the spinner and blocks a second submit while the request is in flight', () => {
    const { fixture, pending } = setup({ pending: true });

    fill(fixture, { current: 'old-secret', next: 'Str0ng-passw0rd', confirm: 'Str0ng-passw0rd' });
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.changingPassword()).toBe(true);
    expect(fixture.nativeElement.querySelector('app-spinner')).not.toBeNull();
    expect(submitButton(fixture).textContent).toContain('Changing...');
    expect(submitButton(fixture).disabled).toBe(true);

    pending.next();
    pending.complete();
    fixture.detectChanges();

    expect(fixture.componentInstance.changingPassword()).toBe(false);
  });

  it('surfaces a rejected current password and keeps the entered values', () => {
    const { fixture, toasts, requests } = setup({ fails: true });

    fill(fixture, { current: 'wrong-one', next: 'Str0ng-passw0rd', confirm: 'Str0ng-passw0rd' });
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(requests).toEqual([{ currentPassword: 'wrong-one', newPassword: 'Str0ng-passw0rd' }]);
    expect(toasts).toEqual(['error:Failed to change password']);
    expect(fixture.componentInstance.passwordForm.currentPassword().value()).toBe('wrong-one');
    expect(fixture.componentInstance.changingPassword()).toBe(false);
  });

  it('clears every field after a successful change', () => {
    const { fixture, toasts, requests } = setup();

    fill(fixture, { current: 'old-secret', next: 'Str0ng-passw0rd', confirm: 'Str0ng-passw0rd' });
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(requests).toEqual([{ currentPassword: 'old-secret', newPassword: 'Str0ng-passw0rd' }]);
    expect(toasts).toEqual(['success:Password changed successfully']);
    expect(fixture.componentInstance.passwordForm.currentPassword().value()).toBe('');
    expect(fixture.componentInstance.passwordForm.newPassword().value()).toBe('');
    expect(fixture.componentInstance.passwordForm.confirmPassword().value()).toBe('');
    expect(submitButton(fixture).disabled).toBe(true);
  });

  it('locks the form and explains why under OIDC exclusive mode', () => {
    const { fixture, requests } = setup({ oidcExclusiveMode: true });

    fill(fixture, { current: 'old-secret', next: 'Str0ng-passw0rd', confirm: 'Str0ng-passw0rd' });

    expect(
      (fixture.nativeElement.querySelector('.section-notice') as HTMLElement).textContent!.trim(),
    ).toBe('Password login is disabled while OIDC exclusive mode is active.');
    expect(submitButton(fixture).disabled).toBe(true);
    expect(requests).toEqual([]);
  });
});
