import { Component, ChangeDetectionStrategy, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SpinnerComponent } from '@ui';
import { AuthService } from '@core/auth/auth.service';

@Component({
  selector: 'app-plex-callback',
  standalone: true,
  imports: [SpinnerComponent],
  template: `
    <div class="plex-callback">
      @if (error()) {
        <p class="plex-callback__error">{{ error() }}</p>
        <p class="plex-callback__redirect">Redirecting to login...</p>
      } @else {
        <app-spinner />
        <p class="plex-callback__message">Completing sign in...</p>
      }
    </div>
  `,
  styles: `
    .plex-callback {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--space-4);
      padding: var(--space-8);
      text-align: center;
    }

    .plex-callback__message {
      color: var(--text-secondary);
      font-size: var(--font-size-sm);
    }

    .plex-callback__error {
      color: var(--color-error);
      font-size: var(--font-size-sm);
    }

    .plex-callback__redirect {
      color: var(--text-secondary);
      font-size: var(--font-size-xs);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlexCallbackComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly error = signal('');

  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private destroyed = false;

  ngOnInit(): void {
    const stored = sessionStorage.getItem('plex_login_pin_id');
    sessionStorage.removeItem('plex_login_pin_id');
    const pinId = Number(stored);

    if (!stored || Number.isNaN(pinId)) {
      this.handleError('Invalid Plex sign-in session');
      return;
    }

    this.pollPin(pinId);
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stopPolling();
  }

  private pollPin(pinId: number): void {
    const deadline = Date.now() + 120_000;
    const poll = () => {
      this.auth.verifyPlexPin(pinId).subscribe({
        next: (result) => {
          if (this.destroyed) {
            return;
          }
          if (result.completed) {
            this.router.navigate(['/dashboard']);
          } else if (Date.now() >= deadline) {
            this.handleError('Plex authorization timed out');
          } else {
            this.pollTimer = setTimeout(poll, 1000);
          }
        },
        error: (err) => {
          if (this.destroyed) {
            return;
          }
          this.handleError(err.message || 'Plex authorization failed');
        },
      });
    };

    poll();
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private handleError(message: string): void {
    this.error.set(message);
    setTimeout(() => this.router.navigate(['/auth/login']), 3000);
  }
}
