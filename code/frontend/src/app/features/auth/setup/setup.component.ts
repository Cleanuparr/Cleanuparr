import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonComponent, InputComponent, SpinnerComponent } from '@ui';
import { AuthService } from '@core/auth/auth.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { tablerCheck, tablerCopy, tablerShieldLock } from '@ng-icons/tabler-icons';

@Component({
  selector: 'app-setup',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, SpinnerComponent, NgIconComponent],
  templateUrl: './setup.component.html',
  styleUrl: './setup.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  viewProviders: [provideIcons({ tablerCheck, tablerCopy, tablerShieldLock })],
})
export class SetupComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  currentStep = signal(1);
  loading = signal(false);
  error = signal('');

  // Step 1 - Account
  username = signal('');
  password = signal('');
  confirmPassword = signal('');

  // Step 2 - 2FA
  totpSecret = signal('');
  qrCodeUri = signal('');
  recoveryCodes = signal<string[]>([]);
  verificationCode = signal('');
  totpVerified = signal(false);
  codesSaved = signal(false);

  // Step 3 - Plex
  plexLinking = signal(false);
  plexLinked = signal(false);
  plexUsername = signal('');
  plexPinId = signal(0);

  get passwordsMatch(): boolean {
    return this.password() === this.confirmPassword();
  }

  get passwordValid(): boolean {
    return this.password().length >= 8;
  }

  // Step 1: Create account
  createAccount(): void {
    if (!this.passwordsMatch || !this.passwordValid) return;

    this.loading.set(true);
    this.error.set('');

    this.auth.createAccount(this.username(), this.password()).subscribe({
      next: () => {
        this.currentStep.set(2);
        this.generateTotp();
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to create account');
        this.loading.set(false);
      },
    });
  }

  // Step 2: Generate TOTP
  private generateTotp(): void {
    this.loading.set(true);
    this.auth.generateTotpSetup().subscribe({
      next: (result) => {
        this.totpSecret.set(result.secret);
        this.qrCodeUri.set(result.qrCodeUri);
        this.recoveryCodes.set(result.recoveryCodes);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to generate 2FA');
        this.loading.set(false);
      },
    });
  }

  verifyTotp(): void {
    this.loading.set(true);
    this.error.set('');

    this.auth.verifyTotpSetup(this.verificationCode()).subscribe({
      next: () => {
        this.totpVerified.set(true);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Invalid code');
        this.loading.set(false);
      },
    });
  }

  goToStep3(): void {
    this.currentStep.set(3);
    this.error.set('');
  }

  copyRecoveryCodes(): void {
    const text = this.recoveryCodes().join('\n');
    navigator.clipboard.writeText(text);
  }

  downloadRecoveryCodes(): void {
    const text = this.recoveryCodes().join('\n');
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'cleanuparr-recovery-codes.txt';
    a.click();
    URL.revokeObjectURL(url);
  }

  // Step 3: Plex linking
  startPlexLink(): void {
    this.plexLinking.set(true);
    this.error.set('');

    this.auth.requestPlexPin().subscribe({
      next: (result) => {
        this.plexPinId.set(result.pinId);
        window.open(result.authUrl, '_blank');
        this.pollPlexPin();
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to start Plex link');
        this.plexLinking.set(false);
      },
    });
  }

  private plexPollTimer: ReturnType<typeof setInterval> | null = null;

  private pollPlexPin(): void {
    let attempts = 0;
    this.plexPollTimer = setInterval(() => {
      attempts++;
      if (attempts > 60) {
        // Timeout after ~2 minutes
        clearInterval(this.plexPollTimer!);
        this.plexLinking.set(false);
        this.error.set('Plex authorization timed out');
        return;
      }

      this.auth.verifyPlexPin(this.plexPinId()).subscribe({
        next: (result) => {
          if (result.completed) {
            clearInterval(this.plexPollTimer!);
            this.plexLinked.set(true);
            this.plexLinking.set(false);
          }
        },
      });
    }, 2000);
  }

  completeSetup(): void {
    this.loading.set(true);
    this.error.set('');

    this.auth.completeSetup().subscribe({
      next: () => {
        this.router.navigate(['/auth/login']);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to complete setup');
        this.loading.set(false);
      },
    });
  }
}
