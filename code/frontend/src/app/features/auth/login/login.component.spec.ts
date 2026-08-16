import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService, LoginResponse, PlexPinResponse, TokenResponse } from '@core/auth/auth.service';
import { LoginComponent } from './login.component';

const TOKENS: TokenResponse = { accessToken: 'access', refreshToken: 'refresh', expiresIn: 900 };
const PIN: PlexPinResponse = { pinId: 42, authUrl: 'https://plex.tv/link' };

interface SetupOptions {
  login?: Observable<LoginResponse>;
  verify2fa?: Observable<TokenResponse>;
  plexPin?: Observable<PlexPinResponse>;
  oidcStart?: Observable<{ authorizationUrl: string }>;
  plexLinked?: boolean;
  oidcEnabled?: boolean;
  oidcExclusiveMode?: boolean;
  queryParams?: Record<string, string>;
}

interface Harness {
  fixture: ComponentFixture<LoginComponent>;
  navigations: string[][];
  loginCalls: [string, string][];
  verify2faCalls: [string, string, boolean | undefined][];
}

describe('LoginComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function setup(options: SetupOptions = {}): Harness {
    const navigations: string[][] = [];
    const loginCalls: [string, string][] = [];
    const verify2faCalls: [string, string, boolean | undefined][] = [];

    const auth = {
      checkStatus: () => of({ setupCompleted: true, plexLinked: options.plexLinked ?? false }),
      login: (username: string, password: string) => {
        loginCalls.push([username, password]);
        return options.login ?? of({ requiresTwoFactor: false, tokens: TOKENS });
      },
      verify2fa: (loginToken: string, code: string, isRecoveryCode?: boolean) => {
        verify2faCalls.push([loginToken, code, isRecoveryCode]);
        return options.verify2fa ?? of(TOKENS);
      },
      requestPlexPin: () => options.plexPin ?? of(PIN),
      startOidcLogin: () => options.oidcStart ?? of({ authorizationUrl: 'https://idp.example/authorize' }),
      plexLinked: signal(options.plexLinked ?? false),
      oidcEnabled: signal(options.oidcEnabled ?? false),
      oidcProviderName: signal('Authentik'),
      oidcExclusiveMode: signal(options.oidcExclusiveMode ?? false),
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
        { provide: ActivatedRoute, useValue: { snapshot: { queryParams: options.queryParams ?? {} } } },
      ],
    });

    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return { fixture, navigations, loginCalls, verify2faCalls };
  }

  function submitButton(fixture: ComponentFixture<LoginComponent>): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.login-submit button') as HTMLButtonElement;
  }

  function errorText(fixture: ComponentFixture<LoginComponent>): string | null {
    const element = fixture.nativeElement.querySelector('.error-message') as HTMLElement | null;
    return element ? element.textContent!.trim() : null;
  }

  it('keeps the submit button disabled until both credentials are filled in', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    expect(submitButton(fixture).disabled).toBe(true);

    component.username.set('admin');
    fixture.detectChanges();
    expect(submitButton(fixture).disabled).toBe(true);

    component.password.set('hunter2');
    fixture.detectChanges();
    expect(submitButton(fixture).disabled).toBe(false);
  });

  it('signs in with a password and lands on the dashboard', () => {
    const { fixture, navigations, loginCalls } = setup();
    const component = fixture.componentInstance;

    component.username.set('admin');
    component.password.set('hunter2');
    fixture.detectChanges();
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(loginCalls).toEqual([['admin', 'hunter2']]);
    expect(navigations).toEqual([['/dashboard']]);
    expect(component.loading()).toBe(false);
    expect(errorText(fixture)).toBeNull();
  });

  it('surfaces rejected credentials without navigating away', () => {
    const { fixture, navigations } = setup({
      login: throwError(() => ({ message: 'Invalid username or password' })),
    });
    const component = fixture.componentInstance;

    component.username.set('admin');
    component.password.set('wrong');
    fixture.detectChanges();
    submitButton(fixture).click();
    fixture.detectChanges();

    expect(errorText(fixture)).toBe('Invalid username or password');
    expect(navigations).toEqual([]);
    expect(component.loading()).toBe(false);
    expect(component.view()).toBe('credentials');
  });

  it('counts down and blocks submitting while the server rate limits sign in', () => {
    vi.useFakeTimers();
    const { fixture } = setup({
      login: throwError(() => ({ message: 'Too many attempts', retryAfterSeconds: 2 })),
    });
    const component = fixture.componentInstance;

    component.username.set('admin');
    component.password.set('hunter2');
    fixture.detectChanges();
    component.submitLogin();
    fixture.detectChanges();

    expect(component.retryCountdown()).toBe(2);
    expect(submitButton(fixture).disabled).toBe(true);

    vi.advanceTimersByTime(1000);
    expect(component.retryCountdown()).toBe(1);

    vi.advanceTimersByTime(1000);
    fixture.detectChanges();
    expect(component.retryCountdown()).toBe(0);
    expect(submitButton(fixture).disabled).toBe(false);
  });

  it('switches to the two factor view when the server asks for a second factor and verifies the code', () => {
    const { fixture, navigations, verify2faCalls } = setup({
      login: of({ requiresTwoFactor: true, loginToken: 'login-token' }),
    });
    const component = fixture.componentInstance;

    component.username.set('admin');
    component.password.set('hunter2');
    fixture.detectChanges();
    component.submitLogin();
    fixture.detectChanges();

    expect(component.view()).toBe('2fa');
    expect(navigations).toEqual([]);
    expect(submitButton(fixture).disabled).toBe(true);

    component.totpCode.set('123456');
    fixture.detectChanges();
    expect(submitButton(fixture).disabled).toBe(false);

    submitButton(fixture).click();
    fixture.detectChanges();

    expect(verify2faCalls).toEqual([['login-token', '123456', undefined]]);
    expect(navigations).toEqual([['/dashboard']]);
  });

  it('accepts a pasted code that carries surrounding whitespace', () => {
    const { fixture, verify2faCalls } = setup({
      login: of({ requiresTwoFactor: true, loginToken: 'login-token' }),
    });
    const component = fixture.componentInstance;

    component.submitLogin();
    component.totpCode.set(' 123456 ');
    fixture.detectChanges();

    expect(submitButton(fixture).disabled).toBe(false);

    submitButton(fixture).click();
    fixture.detectChanges();

    expect(verify2faCalls).toEqual([['login-token', '123456', undefined]]);
  });

  it('verifies a recovery code and reports a rejected one on the recovery view', () => {
    const { fixture, verify2faCalls, navigations } = setup({
      login: of({ requiresTwoFactor: true, loginToken: 'login-token' }),
      verify2fa: throwError(() => ({ message: 'Invalid recovery code' })),
    });
    const component = fixture.componentInstance;

    component.submitLogin();
    component.useRecoveryCode();
    component.recoveryCode.set('ABCD-1234');
    fixture.detectChanges();

    expect(component.view()).toBe('recovery');

    submitButton(fixture).click();
    fixture.detectChanges();

    expect(verify2faCalls).toEqual([['login-token', 'ABCD-1234', true]]);
    expect(errorText(fixture)).toBe('Invalid recovery code');
    expect(navigations).toEqual([]);
  });

  it('counts down when the server rate limits the second factor step', () => {
    vi.useFakeTimers();
    const { fixture } = setup({
      login: of({ requiresTwoFactor: true, loginToken: 'login-token' }),
      verify2fa: throwError(() => ({ message: 'Account is locked', retryAfterSeconds: 2 })),
    });
    const component = fixture.componentInstance;

    component.submitLogin();
    component.totpCode.set('000000');
    fixture.detectChanges();

    component.submit2fa();
    fixture.detectChanges();

    expect(component.retryCountdown()).toBe(2);
    expect(errorText(fixture)).toBe('Account is locked');

    vi.advanceTimersByTime(2000);
    fixture.detectChanges();

    expect(component.retryCountdown()).toBe(0);
  });

  it('hides the Plex and OIDC entry points when the server does not offer them', () => {
    const { fixture } = setup();

    expect(fixture.nativeElement.querySelector('.plex-login-btn')).toBeNull();
    expect(fixture.nativeElement.querySelector('.oidc-login-btn')).toBeNull();
    expect(fixture.nativeElement.querySelector('.divider')).toBeNull();
  });

  it('offers the Plex and OIDC entry points when they are enabled and reports a failed OIDC start', () => {
    const { fixture } = setup({
      plexLinked: true,
      oidcEnabled: true,
      oidcStart: throwError(() => ({ message: 'Provider unreachable' })),
    });

    expect(fixture.nativeElement.querySelector('.plex-login-btn')).not.toBeNull();
    const oidcButton = fixture.nativeElement.querySelector('.oidc-login-btn') as HTMLButtonElement;
    expect(oidcButton.textContent).toContain('Sign in with Authentik');

    oidcButton.click();
    fixture.detectChanges();

    expect(errorText(fixture)).toBe('Provider unreachable');
    expect(fixture.componentInstance.oidcLoading()).toBe(false);
  });

  it('drops the password form in OIDC exclusive mode', () => {
    const { fixture } = setup({ oidcEnabled: true, plexLinked: true, oidcExclusiveMode: true });

    expect(fixture.nativeElement.querySelector('.login-form')).toBeNull();
    expect(fixture.nativeElement.querySelector('.plex-login-btn')).toBeNull();
    expect(fixture.nativeElement.querySelector('.oidc-login-btn')).not.toBeNull();
  });

  it('translates an OIDC error handed back through the query parameters', () => {
    const { fixture } = setup({ queryParams: { oidc_error: 'unauthorized' } });

    expect(errorText(fixture)).toBe('Your account is not authorized for OIDC login');
  });

  it('falls back to a generic message for an unknown OIDC error code', () => {
    const { fixture } = setup({ queryParams: { oidc_error: 'something_else' } });

    expect(errorText(fixture)).toBe('OIDC authentication failed');
  });
});
