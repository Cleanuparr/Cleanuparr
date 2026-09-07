import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AccountApi, ChangeUsernameRequest } from '@core/api/account.api';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ChangeUsernameCardComponent } from './change-username-card.component';

describe('ChangeUsernameCardComponent', () => {
  function setup(options: { fails?: boolean; oidcExclusiveMode?: boolean } = {}) {
    const toasts: string[] = [];
    const requests: ChangeUsernameRequest[] = [];
    let logouts = 0;

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccountApi,
          useValue: {
            changeUsername: (request: ChangeUsernameRequest) => {
              requests.push(request);
              return options.fails ? throwError(() => new Error('boom')) : of(undefined);
            },
          },
        },
        {
          provide: AuthService,
          useValue: { logout: () => logouts++ },
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

    const fixture = TestBed.createComponent(ChangeUsernameCardComponent);
    fixture.componentRef.setInput('currentUsername', 'admin');
    fixture.componentRef.setInput('oidcExclusiveMode', options.oidcExclusiveMode ?? false);
    fixture.detectChanges();
    return { fixture, toasts, requests, logoutCount: () => logouts };
  }

  function fill(
    fixture: ComponentFixture<ChangeUsernameCardComponent>,
    values: { username?: string; password?: string },
  ): void {
    const inputs = Array.from<HTMLInputElement>(fixture.nativeElement.querySelectorAll('input'));
    const entries: [string | undefined, HTMLInputElement][] = [
      [values.username, inputs[0]],
      [values.password, inputs[1]],
    ];
    for (const [value, element] of entries) {
      if (value !== undefined) {
        element.value = value;
        element.dispatchEvent(new Event('input'));
      }
    }
    fixture.detectChanges();
  }

  function submitButton(fixture: ComponentFixture<ChangeUsernameCardComponent>): HTMLButtonElement {
    return Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button.btn')).at(-1)!;
  }

  it('prefills the field with the current username', async () => {
    const { fixture } = setup();
    await fixture.whenStable();

    expect(fixture.componentInstance.newUsername()).toBe('admin');
    expect(Array.from<HTMLInputElement>(fixture.nativeElement.querySelectorAll('input'))[0].value).toBe('admin');
  });

  it('keeps the submit button disabled until the password is filled', () => {
    const { fixture } = setup();

    expect(submitButton(fixture).disabled).toBe(true);

    fill(fixture, { password: 'old-secret' });

    expect(submitButton(fixture).disabled).toBe(false);
  });

  it('blocks the request when the trimmed username is shorter than three characters', () => {
    const { fixture, toasts, requests } = setup();

    fill(fixture, { username: '  ab  ', password: 'old-secret' });
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(requests).toEqual([]);
    expect(toasts).toEqual(['error:Username must be at least 3 characters']);
  });

  it('blocks the request when the username is unchanged', () => {
    const { fixture, toasts, requests } = setup();

    fill(fixture, { password: 'old-secret' });
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(requests).toEqual([]);
    expect(toasts).toEqual(['error:New username must be different from the current username']);
  });

  it('trims the username, signs the user out and asks them to sign in again', () => {
    const { fixture, toasts, requests, logoutCount } = setup();

    fill(fixture, { username: '  renamed  ', password: 'old-secret' });
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(requests).toEqual([{ currentPassword: 'old-secret', newUsername: 'renamed' }]);
    expect(toasts).toEqual(['success:Username changed, please sign in again']);
    expect(logoutCount()).toBe(1);
  });

  it('surfaces a rejected password, keeps the values and stays signed in', () => {
    const { fixture, toasts, requests, logoutCount } = setup({ fails: true });

    fill(fixture, { username: 'renamed', password: 'wrong-one' });
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(requests).toEqual([{ currentPassword: 'wrong-one', newUsername: 'renamed' }]);
    expect(toasts).toEqual(['error:Failed to change username']);
    expect(fixture.componentInstance.newUsername()).toBe('renamed');
    expect(fixture.componentInstance.changingUsername()).toBe(false);
    expect(logoutCount()).toBe(0);
  });

  it('locks the form and explains why under OIDC exclusive mode', () => {
    const { fixture, requests } = setup({ oidcExclusiveMode: true });

    fill(fixture, { username: 'renamed', password: 'old-secret' });

    expect(
      (fixture.nativeElement.querySelector('.section-notice') as HTMLElement).textContent!.trim(),
    ).toBe('Username changes are disabled while OIDC exclusive mode is active.');
    expect(submitButton(fixture).disabled).toBe(true);
    expect(requests).toEqual([]);
  });
});
