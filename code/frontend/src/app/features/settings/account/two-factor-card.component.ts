import { Component, ChangeDetectionStrategy, OnDestroy, inject, input, output, signal } from '@angular/core';
import { CardComponent, InputComponent, ButtonComponent, SpinnerComponent } from '@ui';
import { QRCodeComponent } from 'angularx-qrcode';
import { AccountApi } from '@core/api/account.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';

@Component({
  selector: 'app-two-factor-card',
  standalone: true,
  imports: [CardComponent, InputComponent, ButtonComponent, SpinnerComponent, QRCodeComponent],
  templateUrl: './two-factor-card.component.html',
  styleUrl: './two-factor-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TwoFactorCardComponent implements OnDestroy {
  private readonly api = inject(AccountApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly enabled = input(false);

  /** Emitted after 2FA is enabled or disabled so the parent can reload account info. */
  readonly changed = output<void>();

  // Regeneration + shared setup state
  readonly twoFaPassword = signal('');
  readonly twoFaCode = signal('');
  readonly regenerating2fa = signal(false);
  readonly newRecoveryCodes = signal<string[]>([]);
  readonly newQrCodeUri = signal('');
  readonly newTotpSecret = signal('');

  // Enable flow
  readonly enablePassword = signal('');
  readonly enableVerificationCode = signal('');
  readonly enabling2fa = signal(false);
  readonly enableSetup = signal(false);

  // Disable flow
  readonly disabling2fa = signal(false);

  // Retry countdown, shared by the regenerate and disable forms
  readonly retryCountdown = signal(0);
  private countdownTimer: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy(): void {
    this.clearCountdown();
  }

  async confirmRegenerate2fa(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Regenerate 2FA',
      message: 'This will invalidate your current authenticator setup and all existing recovery codes. You will need to set up your authenticator app again.',
      confirmLabel: 'Regenerate',
      destructive: true,
    });
    if (!confirmed) return;

    this.regenerating2fa.set(true);
    this.api.regenerate2fa({
      password: this.twoFaPassword(),
      totpCode: this.twoFaCode().trim(),
    }).subscribe({
      next: (result) => {
        this.newRecoveryCodes.set(result.recoveryCodes);
        this.newQrCodeUri.set(result.qrCodeUri);
        this.newTotpSecret.set(result.secret);
        this.toast.success('2FA regenerated. Scan the QR code and save your recovery codes!');
        this.twoFaPassword.set('');
        this.twoFaCode.set('');
        this.regenerating2fa.set(false);
      },
      error: (err) => {
        this.toast.error(this.rateLimitMessage(err) ?? 'Failed to regenerate 2FA. Check your password and code.');
        this.regenerating2fa.set(false);
      },
    });
  }

  copyRecoveryCodes(): void {
    const codes = this.newRecoveryCodes().join('\n');
    navigator.clipboard.writeText(codes).then(
      () => this.toast.success('Recovery codes copied to clipboard'),
      () => this.toast.error('Failed to copy recovery codes'),
    );
  }

  dismissRecoveryCodes(): void {
    this.newRecoveryCodes.set([]);
    this.newQrCodeUri.set('');
    this.newTotpSecret.set('');
  }

  startEnable2fa(): void {
    this.enabling2fa.set(true);
    this.api.enable2fa(this.enablePassword()).subscribe({
      next: (result) => {
        this.newQrCodeUri.set(result.qrCodeUri);
        this.newTotpSecret.set(result.secret);
        this.newRecoveryCodes.set(result.recoveryCodes);
        this.enableSetup.set(true);
        this.enabling2fa.set(false);
      },
      error: () => {
        this.toast.error('Failed to start 2FA setup. Check your password.');
        this.enabling2fa.set(false);
      },
    });
  }

  verifyEnable2fa(): void {
    this.enabling2fa.set(true);
    this.api.verifyEnable2fa(this.enableVerificationCode()).subscribe({
      next: () => {
        this.toast.success('Two-factor authentication enabled. Save your recovery codes before dismissing them!');
        this.enableSetup.set(false);
        this.enablePassword.set('');
        this.enableVerificationCode.set('');
        this.newQrCodeUri.set('');
        this.newTotpSecret.set('');
        this.enabling2fa.set(false);
        this.changed.emit();
      },
      error: () => {
        this.toast.error('Invalid verification code');
        this.enabling2fa.set(false);
      },
    });
  }

  cancelEnable2fa(): void {
    this.enableSetup.set(false);
    this.enablePassword.set('');
    this.enableVerificationCode.set('');
    this.newRecoveryCodes.set([]);
    this.newQrCodeUri.set('');
    this.newTotpSecret.set('');
  }

  private rateLimitMessage(err: unknown): string | null {
    const retryAfter = (err as ApiError)?.retryAfterSeconds;
    if (!retryAfter || retryAfter <= 0) {
      return null;
    }

    this.startCountdown(retryAfter);
    return `Too many failed attempts. Try again in ${retryAfter}s.`;
  }

  private startCountdown(seconds: number): void {
    this.clearCountdown();
    this.retryCountdown.set(seconds);
    this.countdownTimer = setInterval(() => {
      const current = this.retryCountdown();
      if (current <= 1) {
        this.clearCountdown();
      } else {
        this.retryCountdown.set(current - 1);
      }
    }, 1000);
  }

  private clearCountdown(): void {
    this.retryCountdown.set(0);
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }

  async confirmDisable2fa(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Disable 2FA',
      message: 'This will remove two-factor authentication from your account. Your recovery codes will be deleted.',
      confirmLabel: 'Disable',
      destructive: true,
    });
    if (!confirmed) return;

    this.disabling2fa.set(true);
    this.api.disable2fa(this.twoFaPassword(), this.twoFaCode().trim()).subscribe({
      next: () => {
        this.toast.success('Two-factor authentication disabled');
        this.twoFaPassword.set('');
        this.twoFaCode.set('');
        this.disabling2fa.set(false);
        this.changed.emit();
      },
      error: (err) => {
        this.toast.error(this.rateLimitMessage(err) ?? 'Failed to disable 2FA. Check your password and code.');
        this.disabling2fa.set(false);
      },
    });
  }
}
