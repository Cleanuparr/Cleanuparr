import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthStatus, AuthService, TotpSetupResponse } from '@core/auth/auth.service';
import { SetupComponent } from './setup.component';

const TOTP: TotpSetupResponse = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUri: 'otpauth://totp/Cleanuparr:admin?secret=JBSWY3DPEHPK3PXP',
  recoveryCodes: ['AAAA-1111', 'BBBB-2222'],
};

const STATUS: AuthStatus = { setupCompleted: true, plexLinked: false };

interface SetupOptions {
  createAccount?: Observable<{ userId: string }>;
  generateTotp?: Observable<TotpSetupResponse>;
  verifyTotp?: Observable<{ message: string }>;
  completeSetup?: Observable<{ message: string }>;
  connectionError?: boolean;
  setupComplete?: boolean;
}

interface Harness {
  fixture: ComponentFixture<SetupComponent>;
  navigations: string[][];
  createAccountCalls: [string, string][];
  verifyTotpCalls: string[];
  connectionError: WritableSignal<boolean>;
}

describe('SetupComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function setup(options: SetupOptions = {}): Harness {
    const navigations: string[][] = [];
    const createAccountCalls: [string, string][] = [];
    const verifyTotpCalls: string[] = [];
    const connectionError = signal(options.connectionError ?? false);
    const setupComplete = signal(options.setupComplete ?? false);

    const auth = {
      connectionError,
      isSetupComplete: setupComplete,
      retryConnection: () => {
        connectionError.set(false);
        return of(STATUS);
      },
      createAccount: (username: string, password: string) => {
        createAccountCalls.push([username, password]);
        return options.createAccount ?? of({ userId: 'user-1' });
      },
      generateTotpSetup: () => options.generateTotp ?? of(TOTP),
      verifyTotpSetup: (code: string) => {
        verifyTotpCalls.push(code);
        return options.verifyTotp ?? of({ message: 'ok' });
      },
      completeSetup: () => options.completeSetup ?? of({ message: 'ok' }),
      requestSetupPlexPin: () => of({ pinId: 1, authUrl: 'https://plex.tv/link' }),
      verifySetupPlexPin: () => of({ completed: false }),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        {
          provide: Router,
          useValue: {
            navigate: (commands: string[]) => {
              navigations.push(commands);
              return Promise.resolve(true);
            },
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(SetupComponent);
    fixture.detectChanges();
    return { fixture, navigations, createAccountCalls, verifyTotpCalls, connectionError };
  }

  function fillAccount(fixture: ComponentFixture<SetupComponent>, password = 'hunter2hunter2'): void {
    const component = fixture.componentInstance;
    component.username.set('admin');
    component.password.set(password);
    component.confirmPassword.set(password);
    fixture.detectChanges();
  }

  function submitButton(fixture: ComponentFixture<SetupComponent>): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.submit-btn button') as HTMLButtonElement;
  }

  function errorText(fixture: ComponentFixture<SetupComponent>): string | null {
    const element = fixture.nativeElement.querySelector('.error-message') as HTMLElement | null;
    return element ? element.textContent!.trim() : null;
  }

  it('grades the password as the user types', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    expect(component.passwordStrength()).toBeNull();

    component.password.set('short');
    expect(component.passwordStrength()).toBe('weak');

    component.password.set('longenough');
    expect(component.passwordStrength()).toBe('weak');

    component.password.set('Longenough1');
    expect(component.passwordStrength()).toBe('medium');

    component.password.set('LongEnough1!pass');
    expect(component.passwordStrength()).toBe('strong');
  });

  it('blocks step one while the password is too short or does not match', () => {
    const { fixture, createAccountCalls } = setup();
    const component = fixture.componentInstance;

    component.username.set('admin');
    component.password.set('short');
    component.confirmPassword.set('short');
    fixture.detectChanges();

    expect(submitButton(fixture).disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Password must be at least 8 characters');

    component.password.set('hunter2hunter2');
    component.confirmPassword.set('hunter2hunter3');
    fixture.detectChanges();

    expect(submitButton(fixture).disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Passwords do not match');

    component.createAccount();
    fixture.detectChanges();

    expect(createAccountCalls).toEqual([]);
    expect(component.currentStep()).toBe(1);
  });

  it('creates the account and generates the 2FA secret on step two', () => {
    const { fixture, createAccountCalls } = setup();
    const component = fixture.componentInstance;

    fillAccount(fixture);
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(createAccountCalls).toEqual([['admin', 'hunter2hunter2']]);
    expect(component.currentStep()).toBe(2);
    expect(component.totpSecret()).toBe(TOTP.secret);
    expect(component.recoveryCodes()).toEqual(TOTP.recoveryCodes);
    expect(component.loading()).toBe(false);
    expect(fixture.nativeElement.querySelector('.qr-secret')!.textContent).toContain(TOTP.secret);
  });

  it('stays on step one when the account cannot be created', () => {
    const { fixture } = setup({ createAccount: throwError(() => ({ message: 'Username already taken' })) });

    fillAccount(fixture);
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.currentStep()).toBe(1);
    expect(errorText(fixture)).toBe('Username already taken');
    expect(fixture.componentInstance.loading()).toBe(false);
  });

  it('reports a rejected verification code and keeps the recovery codes hidden', () => {
    const { fixture, verifyTotpCalls } = setup({ verifyTotp: throwError(() => ({ message: 'Invalid code' })) });
    const component = fixture.componentInstance;

    fillAccount(fixture);
    component.createAccount();
    component.verificationCode.set('000000');
    fixture.detectChanges();

    submitButton(fixture).click();
    fixture.detectChanges();

    expect(verifyTotpCalls).toEqual(['000000']);
    expect(component.totpVerified()).toBe(false);
    expect(errorText(fixture)).toBe('Invalid code');
    expect(fixture.nativeElement.querySelector('.recovery-codes')).toBeNull();
  });

  it('gates the step three advance behind the saved recovery codes checkbox', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    fillAccount(fixture);
    component.createAccount();
    component.verificationCode.set('123456');
    fixture.detectChanges();

    submitButton(fixture).click();
    fixture.detectChanges();

    expect(component.totpVerified()).toBe(true);
    const codes = Array.from(fixture.nativeElement.querySelectorAll('.recovery-code')).map((code) =>
      (code as HTMLElement).textContent!.trim(),
    );
    expect(codes).toEqual(TOTP.recoveryCodes);
    expect(submitButton(fixture).disabled).toBe(true);

    const checkbox = fixture.nativeElement.querySelector('.checkbox-label input') as HTMLInputElement;
    checkbox.click();
    fixture.detectChanges();

    expect(component.codesSaved()).toBe(true);
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(component.currentStep()).toBe(3);
  });

  it('lets the user skip 2FA and finish the setup from step three', () => {
    const { fixture, navigations } = setup();
    const component = fixture.componentInstance;

    fillAccount(fixture);
    component.createAccount();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.skip-link') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(component.currentStep()).toBe(3);
    expect(component.totpVerified()).toBe(false);

    submitButton(fixture).click();
    fixture.detectChanges();

    expect(navigations).toEqual([['/auth/login']]);
  });

  it('keeps the user on step three when completing the setup fails', () => {
    const { fixture, navigations } = setup({ completeSetup: throwError(() => ({ message: 'Server unavailable' })) });
    const component = fixture.componentInstance;

    component.currentStep.set(3);
    fixture.detectChanges();

    submitButton(fixture).click();
    fixture.detectChanges();

    expect(navigations).toEqual([]);
    expect(component.currentStep()).toBe(3);
    expect(errorText(fixture)).toBe('Server unavailable');
    expect(component.loading()).toBe(false);
  });

  it('replaces the wizard with a retry prompt while the server is unreachable', () => {
    vi.useFakeTimers();
    const { fixture, navigations } = setup({ connectionError: true, setupComplete: true });

    expect(fixture.nativeElement.textContent).toContain('Could not connect to server');
    expect(fixture.nativeElement.querySelector('.steps-indicator')).toBeNull();

    fixture.componentInstance.retryConnection();
    fixture.detectChanges();
    expect(fixture.componentInstance.retrying()).toBe(true);

    vi.advanceTimersByTime(500);
    fixture.detectChanges();

    expect(fixture.componentInstance.retrying()).toBe(false);
    expect(navigations).toEqual([['/auth/login']]);
  });
});
