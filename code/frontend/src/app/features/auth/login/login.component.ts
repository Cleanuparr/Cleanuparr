import { Component, ChangeDetectionStrategy, inject, signal, viewChild, effect, afterNextRender } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonComponent, InputComponent, SpinnerComponent } from '@ui';
import { AuthService } from '@core/auth/auth.service';

type LoginView = 'credentials' | '2fa' | 'recovery';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, SpinnerComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  view = signal<LoginView>('credentials');
  loading = signal(false);
  error = signal('');

  // Credentials
  username = signal('');
  password = signal('');

  // 2FA
  loginToken = signal('');
  totpCode = signal('');
  recoveryCode = signal('');

  // Plex
  plexLinked = this.auth.plexLinked;
  plexLoading = signal(false);
  plexPinId = signal(0);

  // Auto-focus refs
  usernameInput = viewChild<InputComponent>('usernameInput');
  totpInput = viewChild<InputComponent>('totpInput');
  recoveryInput = viewChild<InputComponent>('recoveryInput');

  constructor() {
    // Auto-focus username input on initial render
    afterNextRender(() => {
      this.usernameInput()?.focus();
    });

    // Auto-focus on view change
    effect(() => {
      const currentView = this.view();
      if (currentView === '2fa') {
        setTimeout(() => this.totpInput()?.focus());
      } else if (currentView === 'recovery') {
        setTimeout(() => this.recoveryInput()?.focus());
      }
    });
  }

  submitLogin(): void {
    this.loading.set(true);
    this.error.set('');

    this.auth.login(this.username(), this.password()).subscribe({
      next: (result) => {
        if (result.requiresTwoFactor && result.loginToken) {
          this.loginToken.set(result.loginToken);
          this.view.set('2fa');
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Invalid credentials');
        this.loading.set(false);
      },
    });
  }

  submit2fa(): void {
    this.loading.set(true);
    this.error.set('');

    this.auth.verify2fa(this.loginToken(), this.totpCode()).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.error.set(err.message || 'Invalid code');
        this.loading.set(false);
      },
    });
  }

  submitRecoveryCode(): void {
    this.loading.set(true);
    this.error.set('');

    this.auth.verify2fa(this.loginToken(), this.recoveryCode(), true).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.error.set(err.message || 'Invalid recovery code');
        this.loading.set(false);
      },
    });
  }

  useRecoveryCode(): void {
    this.view.set('recovery');
    this.error.set('');
  }

  backTo2fa(): void {
    this.view.set('2fa');
    this.error.set('');
  }

  backToCredentials(): void {
    this.view.set('credentials');
    this.error.set('');
    this.loginToken.set('');
  }

  private plexPollTimer: ReturnType<typeof setInterval> | null = null;

  startPlexLogin(): void {
    this.plexLoading.set(true);
    this.error.set('');

    this.auth.requestPlexPin().subscribe({
      next: (result) => {
        this.plexPinId.set(result.pinId);
        window.open(result.authUrl, '_blank');
        this.pollPlexPin();
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to start Plex login');
        this.plexLoading.set(false);
      },
    });
  }

  private pollPlexPin(): void {
    let attempts = 0;
    this.plexPollTimer = setInterval(() => {
      attempts++;
      if (attempts > 60) {
        clearInterval(this.plexPollTimer!);
        this.plexLoading.set(false);
        this.error.set('Plex authorization timed out');
        return;
      }

      this.auth.verifyPlexPin(this.plexPinId()).subscribe({
        next: (result) => {
          if (result.completed) {
            clearInterval(this.plexPollTimer!);
            this.plexLoading.set(false);
            this.router.navigate(['/dashboard']);
          }
        },
      });
    }, 2000);
  }
}
