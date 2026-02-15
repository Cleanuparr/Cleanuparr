import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, SpinnerComponent,
  EmptyStateComponent, LoadingStateComponent,
} from '@ui';
import { AccountApi, AccountInfo } from '@core/api/account.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { DeferredLoader } from '@shared/utils/loading.util';
import { QRCodeComponent } from 'angularx-qrcode';

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    SpinnerComponent, EmptyStateComponent, LoadingStateComponent, QRCodeComponent,
  ],
  templateUrl: './account-settings.component.html',
  styleUrl: './account-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountSettingsComponent implements OnInit, OnDestroy {
  private readonly api = inject(AccountApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly account = signal<AccountInfo | null>(null);

  // Change password
  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly changingPassword = signal(false);

  // Password strength
  readonly newPasswordStrength = computed(() => {
    const pw = this.newPassword();
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

  // 2FA regeneration
  readonly twoFaPassword = signal('');
  readonly twoFaCode = signal('');
  readonly regenerating2fa = signal(false);
  readonly newRecoveryCodes = signal<string[]>([]);
  readonly newQrCodeUri = signal('');
  readonly newTotpSecret = signal('');

  // API key
  readonly apiKey = signal('');
  readonly apiKeyRevealed = signal(false);
  readonly regeneratingApiKey = signal(false);

  // Plex
  readonly plexLinking = signal(false);
  readonly plexUnlinking = signal(false);
  private plexPollTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.loadAccount();
  }

  ngOnDestroy(): void {
    if (this.plexPollTimer) {
      clearInterval(this.plexPollTimer);
    }
  }

  private loadAccount(): void {
    this.loader.start();
    this.api.getInfo().subscribe({
      next: (info) => {
        this.account.set(info);
        this.loader.stop();
      },
      error: () => {
        this.toast.error('Failed to load account information');
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadAccount();
  }

  // Change password
  changePassword(): void {
    if (this.newPassword() !== this.confirmPassword()) {
      this.toast.error('Passwords do not match');
      return;
    }
    if (this.newPassword().length < 8) {
      this.toast.error('Password must be at least 8 characters');
      return;
    }

    this.changingPassword.set(true);
    this.api.changePassword({
      currentPassword: this.currentPassword(),
      newPassword: this.newPassword(),
    }).subscribe({
      next: () => {
        this.toast.success('Password changed successfully');
        this.currentPassword.set('');
        this.newPassword.set('');
        this.confirmPassword.set('');
        this.changingPassword.set(false);
      },
      error: () => {
        this.toast.error('Failed to change password');
        this.changingPassword.set(false);
      },
    });
  }

  // 2FA regeneration
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
      totpCode: this.twoFaCode(),
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
      error: () => {
        this.toast.error('Failed to regenerate 2FA. Check your password and code.');
        this.regenerating2fa.set(false);
      },
    });
  }

  copyRecoveryCodes(): void {
    const codes = this.newRecoveryCodes().join('\n');
    navigator.clipboard.writeText(codes);
    this.toast.success('Recovery codes copied to clipboard');
  }

  dismissRecoveryCodes(): void {
    this.newRecoveryCodes.set([]);
    this.newQrCodeUri.set('');
    this.newTotpSecret.set('');
  }

  // API key
  revealApiKey(): void {
    if (this.apiKeyRevealed()) {
      this.apiKeyRevealed.set(false);
      this.apiKey.set('');
      return;
    }

    this.api.getApiKey().subscribe({
      next: (result) => {
        this.apiKey.set(result.apiKey);
        this.apiKeyRevealed.set(true);
      },
      error: () => this.toast.error('Failed to load API key'),
    });
  }

  copyApiKey(): void {
    navigator.clipboard.writeText(this.apiKey());
    this.toast.success('API key copied to clipboard');
  }

  async confirmRegenerateApiKey(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Regenerate API Key',
      message: 'This will invalidate the current API key. Any integrations using this key will stop working.',
      confirmLabel: 'Regenerate',
      destructive: true,
    });
    if (!confirmed) return;

    this.regeneratingApiKey.set(true);
    this.api.regenerateApiKey().subscribe({
      next: (result) => {
        this.apiKey.set(result.apiKey);
        this.apiKeyRevealed.set(true);
        this.toast.success('API key regenerated');
        this.regeneratingApiKey.set(false);
      },
      error: () => {
        this.toast.error('Failed to regenerate API key');
        this.regeneratingApiKey.set(false);
      },
    });
  }

  // Plex
  startPlexLink(): void {
    this.plexLinking.set(true);
    this.api.linkPlex().subscribe({
      next: (result) => {
        window.open(result.authUrl, '_blank');
        this.pollPlexLink(result.pinId);
      },
      error: () => {
        this.toast.error('Failed to start Plex linking');
        this.plexLinking.set(false);
      },
    });
  }

  private pollPlexLink(pinId: number): void {
    let attempts = 0;
    this.plexPollTimer = setInterval(() => {
      attempts++;
      if (attempts > 60) {
        clearInterval(this.plexPollTimer!);
        this.plexLinking.set(false);
        this.toast.error('Plex linking timed out');
        return;
      }

      this.api.verifyPlexLink(pinId).subscribe({
        next: (result) => {
          if (result.completed) {
            clearInterval(this.plexPollTimer!);
            this.plexLinking.set(false);
            this.toast.success('Plex account linked');
            this.loadAccount();
          }
        },
        error: () => {
          clearInterval(this.plexPollTimer!);
          this.plexLinking.set(false);
          this.toast.error('Plex linking failed');
        },
      });
    }, 2000);
  }

  async confirmUnlinkPlex(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Unlink Plex',
      message: 'This will remove your linked Plex account. You will no longer be able to log in with Plex.',
      confirmLabel: 'Unlink',
      destructive: true,
    });
    if (!confirmed) return;

    this.plexUnlinking.set(true);
    this.api.unlinkPlex().subscribe({
      next: () => {
        this.toast.success('Plex account unlinked');
        this.plexUnlinking.set(false);
        this.loadAccount();
      },
      error: () => {
        this.toast.error('Failed to unlink Plex account');
        this.plexUnlinking.set(false);
      },
    });
  }
}
