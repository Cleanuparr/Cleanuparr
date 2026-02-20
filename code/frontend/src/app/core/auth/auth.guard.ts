import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Waits for the initial auth status check to complete,
 * then evaluates the guard condition.
 */
function waitForAuth(guardFn: (auth: AuthService, router: Router) => boolean | import('@angular/router').UrlTree) {
  const fn: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    // If already loaded, evaluate immediately
    if (!auth.isLoading()) {
      return guardFn(auth, router);
    }

    // Wait for loading to complete
    return toObservable(auth.isLoading).pipe(
      filter((loading) => !loading),
      take(1),
      map(() => guardFn(auth, router)),
    );
  };
  return fn;
}

export const authGuard: CanActivateFn = waitForAuth((auth, router) => {
  if (!auth.isSetupComplete()) {
    return router.createUrlTree(['/auth/setup']);
  }
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/auth/login']);
  }
  return true;
});

export const setupIncompleteGuard: CanActivateFn = waitForAuth((auth, router) => {
  if (auth.isSetupComplete()) {
    return router.createUrlTree(['/auth/login']);
  }
  return true;
});

export const loginGuard: CanActivateFn = waitForAuth((auth, router) => {
  if (!auth.isSetupComplete()) {
    return router.createUrlTree(['/auth/setup']);
  }
  if (auth.isAuthenticated()) {
    return router.createUrlTree(['/dashboard']);
  }
  return true;
});
