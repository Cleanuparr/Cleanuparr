import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService, PlexVerifyResponse } from '@core/auth/auth.service';
import { PlexCallbackComponent } from './plex-callback.component';

const PIN_KEY = 'plex_login_pin_id';
const TOKENS = { accessToken: 'access', refreshToken: 'refresh', expiresIn: 900 };

interface SetupOptions {
  storedPin?: string;
  responses?: Observable<PlexVerifyResponse>[];
}

interface Harness {
  fixture: ComponentFixture<PlexCallbackComponent>;
  navigations: string[][];
  verifiedPins: number[];
}

describe('PlexCallbackComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
    sessionStorage.clear();
  });

  function setup(options: SetupOptions = {}): Harness {
    const navigations: string[][] = [];
    const verifiedPins: number[] = [];
    const responses = [...(options.responses ?? [of({ completed: true, tokens: TOKENS })])];

    if (options.storedPin !== undefined) {
      sessionStorage.setItem(PIN_KEY, options.storedPin);
    }

    const auth = {
      verifyPlexPin: (pinId: number) => {
        verifiedPins.push(pinId);
        return responses.length > 1 ? responses.shift()! : responses[0];
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
      ],
    });

    const fixture = TestBed.createComponent(PlexCallbackComponent);
    fixture.detectChanges();
    return { fixture, navigations, verifiedPins };
  }

  function errorText(fixture: ComponentFixture<PlexCallbackComponent>): string | null {
    const element = fixture.nativeElement.querySelector('.plex-callback__error') as HTMLElement | null;
    return element ? element.textContent!.trim() : null;
  }

  it('verifies the stored pin, clears it and continues to the dashboard', () => {
    const { fixture, navigations, verifiedPins } = setup({ storedPin: '4242' });

    expect(verifiedPins).toEqual([4242]);
    expect(sessionStorage.getItem(PIN_KEY)).toBeNull();
    expect(navigations).toEqual([['/dashboard']]);
    expect(errorText(fixture)).toBeNull();
  });

  it('keeps polling until Plex reports the pin as authorized', () => {
    vi.useFakeTimers();
    const { navigations, verifiedPins } = setup({
      storedPin: '4242',
      responses: [of({ completed: false }), of({ completed: true, tokens: TOKENS })],
    });

    expect(verifiedPins).toEqual([4242]);
    expect(navigations).toEqual([]);

    vi.advanceTimersByTime(1000);

    expect(verifiedPins).toEqual([4242, 4242]);
    expect(navigations).toEqual([['/dashboard']]);
  });

  it('reports an invalid sign in session when no pin was stored', () => {
    vi.useFakeTimers();
    const { fixture, navigations, verifiedPins } = setup();

    expect(verifiedPins).toEqual([]);
    expect(errorText(fixture)).toBe('Invalid Plex sign-in session');
    expect(fixture.nativeElement.textContent).toContain('Redirecting to login...');

    vi.advanceTimersByTime(3000);
    expect(navigations).toEqual([['/auth/login']]);
  });

  it('reports a rejected verification', () => {
    vi.useFakeTimers();
    const { fixture, navigations } = setup({
      storedPin: '4242',
      responses: [throwError(() => ({ message: 'Plex rejected the pin' }))],
    });

    expect(errorText(fixture)).toBe('Plex rejected the pin');
    expect(navigations).toEqual([]);
  });

  it('gives up once the authorization window has elapsed', () => {
    vi.useFakeTimers();
    const { fixture, navigations } = setup({ storedPin: '4242', responses: [of({ completed: false })] });

    vi.advanceTimersByTime(121_000);
    fixture.detectChanges();

    expect(errorText(fixture)).toBe('Plex authorization timed out');

    vi.advanceTimersByTime(3000);
    expect(navigations).toEqual([['/auth/login']]);
  });

  it('stops polling once the page is destroyed', () => {
    vi.useFakeTimers();
    const { fixture, verifiedPins } = setup({ storedPin: '4242', responses: [of({ completed: false })] });

    expect(verifiedPins).toEqual([4242]);

    fixture.destroy();
    vi.advanceTimersByTime(5000);

    expect(verifiedPins).toEqual([4242]);
  });
});
