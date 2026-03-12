import { Component, ChangeDetectionStrategy, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SpinnerComponent } from '@ui';
import { AuthService } from '@core/auth/auth.service';

@Component({
  selector: 'app-oidc-callback',
  standalone: true,
  imports: [SpinnerComponent],
  template: `
    <div class="oidc-callback">
      @if (error()) {
        <p class="oidc-callback__error">{{ error() }}</p>
        <p class="oidc-callback__redirect">Redirecting to login...</p>
      } @else {
        <app-spinner />
        <p class="oidc-callback__message">Completing sign in...</p>
      }
    </div>
  `,
  styles: `
    .oidc-callback {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--space-4);
      padding: var(--space-8);
      text-align: center;
    }

    .oidc-callback__message {
      color: var(--text-secondary);
      font-size: var(--font-size-sm);
    }

    .oidc-callback__error {
      color: var(--color-error);
      font-size: var(--font-size-sm);
    }

    .oidc-callback__redirect {
      color: var(--text-secondary);
      font-size: var(--font-size-xs);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OidcCallbackComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly error = signal('');

  ngOnInit(): void {
    const params = this.route.snapshot.queryParams;
    const code = params['code'];
    const oidcError = params['oidc_error'];

    if (oidcError) {
      this.handleError(oidcError);
      return;
    }

    if (!code) {
      this.handleError('missing_code');
      return;
    }

    this.auth.exchangeOidcCode(code).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: () => {
        this.handleError('exchange_failed');
      },
    });
  }

  private handleError(errorCode: string): void {
    const messages: Record<string, string> = {
      provider_error: 'The identity provider returned an error',
      invalid_request: 'Invalid authentication request',
      authentication_failed: 'Authentication failed',
      unauthorized: 'Your account is not authorized for OIDC login',
      no_account: 'No account found',
      missing_code: 'Invalid callback - missing authorization code',
      exchange_failed: 'Failed to complete sign in',
    };

    this.error.set(messages[errorCode] || 'An unknown error occurred');

    setTimeout(() => {
      this.router.navigate(['/auth/login']);
    }, 3000);
  }
}
