import { signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';
import { firstValueFrom, isObservable, Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { authGuard, loginGuard, setupIncompleteGuard } from './auth.guard';

describe('auth guards', () => {
  interface AuthStub {
    isLoading: WritableSignal<boolean>;
    isSetupComplete: WritableSignal<boolean>;
    isAuthenticated: WritableSignal<boolean>;
  }

  interface SetupOptions {
    loading?: boolean;
    setupComplete?: boolean;
    authenticated?: boolean;
  }

  const route = {} as ActivatedRouteSnapshot;
  const state = {} as RouterStateSnapshot;

  function setup(options: SetupOptions = {}): AuthStub {
    const auth: AuthStub = {
      isLoading: signal(options.loading ?? false),
      isSetupComplete: signal(options.setupComplete ?? true),
      isAuthenticated: signal(options.authenticated ?? true),
    };

    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });

    return auth;
  }

  function invoke(guard: CanActivateFn): boolean | UrlTree | Observable<boolean | UrlTree> {
    const result = TestBed.runInInjectionContext(() => guard(route, state));
    return result as boolean | UrlTree | Observable<boolean | UrlTree>;
  }

  function target(guard: CanActivateFn): string {
    const result = invoke(guard);
    return String(result);
  }

  describe('authGuard', () => {
    it('redirects to setup when setup is incomplete', () => {
      setup({ setupComplete: false });

      expect(target(authGuard)).toBe('/auth/setup');
    });

    it('redirects to login when setup is complete but the user is signed out', () => {
      setup({ authenticated: false });

      expect(target(authGuard)).toBe('/auth/login');
    });

    it('allows an authenticated user through', () => {
      setup();

      expect(invoke(authGuard)).toBe(true);
    });
  });

  describe('setupIncompleteGuard', () => {
    it('redirects to login once setup is complete', () => {
      setup();

      expect(target(setupIncompleteGuard)).toBe('/auth/login');
    });

    it('allows access while setup is incomplete', () => {
      setup({ setupComplete: false });

      expect(invoke(setupIncompleteGuard)).toBe(true);
    });
  });

  describe('loginGuard', () => {
    it('redirects to setup when setup is incomplete', () => {
      setup({ setupComplete: false });

      expect(target(loginGuard)).toBe('/auth/setup');
    });

    it('redirects an already authenticated user to the dashboard', () => {
      setup();

      expect(target(loginGuard)).toBe('/dashboard');
    });

    it('allows a signed out user to reach the login page', () => {
      setup({ authenticated: false });

      expect(invoke(loginGuard)).toBe(true);
    });
  });

  describe('while the initial auth check is running', () => {
    it('defers the decision until loading completes', async () => {
      const auth = setup({ loading: true, authenticated: false });

      const result = invoke(authGuard);
      if (!isObservable(result)) {
        throw new Error('expected the guard to wait for the auth check');
      }
      const decision = firstValueFrom(result);

      auth.isAuthenticated.set(true);
      auth.isLoading.set(false);
      TestBed.tick();

      expect(await decision).toBe(true);
    });

    it('emits a redirect when the auth check resolves to a signed out user', async () => {
      const auth = setup({ loading: true, authenticated: false });

      const result = invoke(authGuard);
      if (!isObservable(result)) {
        throw new Error('expected the guard to wait for the auth check');
      }
      const decision = firstValueFrom(result);

      auth.isLoading.set(false);
      TestBed.tick();

      expect(String(await decision)).toBe('/auth/login');
    });
  });
});
