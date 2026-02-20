import { Component, ChangeDetectionStrategy, inject, signal, computed, viewChild, effect, afterNextRender, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonComponent, InputComponent, SpinnerComponent } from '@ui';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/services/toast.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { tablerCheck, tablerCopy, tablerShieldLock } from '@ng-icons/tabler-icons';
import { QRCodeComponent } from 'angularx-qrcode';

@Component({
  selector: 'app-setup',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, SpinnerComponent, NgIconComponent, QRCodeComponent],
  templateUrl: './setup.component.html',
  styleUrl: './setup.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  viewProviders: [provideIcons({ tablerCheck, tablerCopy, tablerShieldLock })],
})
export class SetupComponent implements OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

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

  // Auto-focus refs
  usernameInput = viewChild<InputComponent>('usernameInput');
  verificationInput = viewChild<InputComponent>('verificationInput');

  // Password strength
  passwordStrength = computed(() => {
    const pw = this.password();
    if (!pw) return null;
    if (pw.length < 8) return 'weak';
    const hasUpper = /[A-Z]/.test(pw);
    const hasLower = /[a-z]/.test(pw);
    const hasNumber = /[0-9]/.test(pw);
    const hasSpecial = /[^A-Za-z0-9]/.test(pw);
    const score = [hasUpper, hasLower, hasNumber, hasSpecial].filter(Boolean).length;
    if (pw.length >= 12 && score >= 3) return 'strong';
    if (pw.length >= 8 && score >= 2) return 'medium';
    return 'weak';
  });

  get passwordsMatch(): boolean {
    return this.password() === this.confirmPassword();
  }

  get passwordValid(): boolean {
    return this.password().length >= 8;
  }

  constructor() {
    // Auto-focus username input on initial render
    afterNextRender(() => {
      this.usernameInput()?.focus();
    });

    // Auto-focus verification input when entering step 2
    effect(() => {
      const step = this.currentStep();
      if (step === 2) {
        setTimeout(() => this.verificationInput()?.focus());
      }
    });
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
    this.toast.success('Recovery codes copied to clipboard');
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

    this.auth.requestSetupPlexPin().subscribe({
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

  ngOnDestroy(): void {
    if (this.plexPollTimer) {
      clearInterval(this.plexPollTimer);
    }
  }

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

      this.auth.verifySetupPlexPin(this.plexPinId()).subscribe({
        next: (result) => {
          if (result.completed) {
            clearInterval(this.plexPollTimer!);
            this.plexLinked.set(true);
            this.plexLinking.set(false);
          }
        },
        error: (err) => {
          clearInterval(this.plexPollTimer!);
          this.plexLinking.set(false);
          this.error.set(err.message || 'Plex linking failed');
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
