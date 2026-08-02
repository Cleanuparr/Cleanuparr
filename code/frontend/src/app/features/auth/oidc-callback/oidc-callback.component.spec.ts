import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService, TokenResponse } from '@core/auth/auth.service';
import { OidcCallbackComponent } from './oidc-callback.component';

const TOKENS: TokenResponse = { accessToken: 'access', refreshToken: 'refresh', expiresIn: 900 };

interface SetupOptions {
  queryParams?: Record<string, string>;
  exchange?: Observable<TokenResponse>;
}

interface Harness {
  fixture: ComponentFixture<OidcCallbackComponent>;
  navigations: string[][];
  exchangedCodes: string[];
}

describe('OidcCallbackComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function setup(options: SetupOptions = {}): Harness {
    const navigations: string[][] = [];
    const exchangedCodes: string[] = [];

    const auth = {
      exchangeOidcCode: (code: string) => {
        exchangedCodes.push(code);
        return options.exchange ?? of(TOKENS);
      },
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

    const fixture = TestBed.createComponent(OidcCallbackComponent);
    fixture.detectChanges();
    return { fixture, navigations, exchangedCodes };
  }

  function errorText(fixture: ComponentFixture<OidcCallbackComponent>): string | null {
    const element = fixture.nativeElement.querySelector('.oidc-callback__error') as HTMLElement | null;
    return element ? element.textContent!.trim() : null;
  }

  it('exchanges the authorization code and continues to the dashboard', () => {
    const { fixture, navigations, exchangedCodes } = setup({ queryParams: { code: 'auth-code' } });

    expect(exchangedCodes).toEqual(['auth-code']);
    expect(navigations).toEqual([['/dashboard']]);
    expect(errorText(fixture)).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Completing sign in...');
  });

  it('reports a missing authorization code and returns to login after the delay', () => {
    vi.useFakeTimers();
    const { fixture, navigations, exchangedCodes } = setup();

    expect(exchangedCodes).toEqual([]);
    expect(errorText(fixture)).toBe('Invalid callback - missing authorization code');
    expect(navigations).toEqual([]);

    vi.advanceTimersByTime(3000);
    expect(navigations).toEqual([['/auth/login']]);
  });

  it('shows the provider error instead of exchanging the code', () => {
    const { fixture, exchangedCodes } = setup({
      queryParams: { code: 'auth-code', oidc_error: 'unauthorized' },
    });

    expect(exchangedCodes).toEqual([]);
    expect(errorText(fixture)).toBe('Your account is not authorized for OIDC login');
    expect(fixture.nativeElement.textContent).toContain('Redirecting to login...');
  });

  it('falls back to a generic message for an unknown provider error code', () => {
    const { fixture } = setup({ queryParams: { oidc_error: 'teapot' } });

    expect(errorText(fixture)).toBe('An unknown error occurred');
  });

  it('reports a rejected code exchange', () => {
    const { fixture, navigations } = setup({
      queryParams: { code: 'expired-code' },
      exchange: throwError(() => ({ message: 'code expired' })),
    });

    expect(errorText(fixture)).toBe('Failed to complete sign in');
    expect(navigations).toEqual([]);
  });
});
