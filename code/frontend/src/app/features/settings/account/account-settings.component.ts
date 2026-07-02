import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, OnInit } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, FormField } from '@angular/forms/signals';
import { ActivatedRoute } from '@angular/router';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, SpinnerComponent,
  ToggleComponent, LabelComponent,
  EmptyStateComponent, LoadingStateComponent,
} from '@ui';
import { forkJoin } from 'rxjs';
import { AccountApi } from '@core/api/account.api';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { DeferredLoader } from '@shared/utils/loading.util';
import { QRCodeComponent } from 'angularx-qrcode';
import { ApiKeyCardComponent } from './api-key-card.component';
import { ChangePasswordCardComponent } from './change-password-card.component';
import { PlexIntegrationCardComponent } from './plex-integration-card.component';

interface OidcFormModel {
  enabled: boolean;
  issuerUrl: string;
  clientId: string;
  clientSecret: string;
  scopes: string;
  providerName: string;
  redirectUrl: string;
  authorizedSubject: string;
  exclusiveMode: boolean;
}

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    SpinnerComponent, ToggleComponent,
    EmptyStateComponent, LoadingStateComponent, QRCodeComponent, LabelComponent, FormField,
    ApiKeyCardComponent, ChangePasswordCardComponent, PlexIntegrationCardComponent,
  ],
  templateUrl: './account-settings.component.html',
  styleUrl: './account-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountSettingsComponent implements OnInit {
  private readonly api = inject(AccountApi);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  private readonly route = inject(ActivatedRoute);

  private readonly accountResource = rxResource({
    stream: () => forkJoin([this.api.getInfo(), this.api.getOidcConfig()]),
  });

  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.accountResource.error());
  readonly account = computed(() => this.accountResource.hasValue() ? this.accountResource.value()[0] : null);

  // 2FA regeneration
  readonly twoFaPassword = signal('');
  readonly twoFaCode = signal('');
  readonly regenerating2fa = signal(false);
  readonly newRecoveryCodes = signal<string[]>([]);
  readonly newQrCodeUri = signal('');
  readonly newTotpSecret = signal('');

  // 2FA enable
  readonly enablePassword = signal('');
  readonly enableVerificationCode = signal('');
  readonly enabling2fa = signal(false);
  readonly enableSetup = signal(false);

  // 2FA disable
  readonly disabling2fa = signal(false);

  // OIDC
  private readonly oidcModel = signal<OidcFormModel>({
    enabled: false,
    issuerUrl: '',
    clientId: '',
    clientSecret: '',
    scopes: 'openid profile email',
    providerName: 'OIDC',
    redirectUrl: '',
    authorizedSubject: '',
    exclusiveMode: false,
  });
  readonly oidcForm = form(this.oidcModel, (p) => {
    required(p.issuerUrl, { when: ({ valueOf }) => valueOf(p.enabled), message: 'Issuer URL is required' });
    required(p.clientId, { when: ({ valueOf }) => valueOf(p.enabled), message: 'Client ID is required' });
  });
  readonly oidcExclusiveMode = computed(() => this.oidcModel().exclusiveMode);
  readonly oidcAuthorizedSubject = computed(() => this.oidcModel().authorizedSubject);
  readonly oidcExpanded = signal(false);
  readonly oidcLinking = signal(false);
  readonly oidcUnlinking = signal(false);
  readonly oidcSaving = signal(false);
  readonly oidcSaved = signal(false);

  constructor() {
    // Reset exclusive mode when OIDC is toggled off. Guard on exclusiveMode too:
    // the write flips it false, so the effect settles instead of writing a fresh
    // object every run (which would loop forever while enabled stays false).
    effect(() => {
      const m = this.oidcModel();
      if (!m.enabled && m.exclusiveMode) {
        untracked(() => this.oidcModel.update(mm => ({ ...mm, exclusiveMode: false })));
      }
    });

    effect(() => {
      const data = this.accountResource.hasValue() ? this.accountResource.value() : undefined;
      if (!data) {
        return;
      }
      const oidc = data[1];
      untracked(() => {
        this.oidcModel.set({
          enabled: oidc.enabled,
          issuerUrl: oidc.issuerUrl,
          clientId: oidc.clientId,
          clientSecret: oidc.clientSecret,
          scopes: oidc.scopes || 'openid profile email',
          providerName: oidc.providerName || 'OIDC',
          redirectUrl: oidc.redirectUrl || '',
          authorizedSubject: oidc.authorizedSubject,
          exclusiveMode: oidc.exclusiveMode,
        });
      });
    });

    effect(() => {
      if (this.accountResource.error()) {
        this.toast.error('Failed to load account information');
      }
    });

    effect(() => {
      if (this.accountResource.isLoading()) {
        this.loader.start();
      } else {
        this.loader.stop();
      }
    });
  }

  ngOnInit(): void {
    const params = this.route.snapshot.queryParams;
    if (params['oidc_link'] === 'success') {
      this.toast.success('OIDC account linked successfully');
      this.oidcExpanded.set(true);
    } else if (params['oidc_link_error']) {
      this.toast.error('Failed to link OIDC account');
      this.oidcExpanded.set(true);
    }
  }

  retry(): void {
    this.accountResource.reload();
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

  // 2FA enable flow
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
        this.toast.success('Two-factor authentication enabled');
        this.cancelEnable2fa();
        this.enabling2fa.set(false);
        this.accountResource.reload();
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

  // 2FA disable flow
  async confirmDisable2fa(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Disable 2FA',
      message: 'This will remove two-factor authentication from your account. Your recovery codes will be deleted.',
      confirmLabel: 'Disable',
      destructive: true,
    });
    if (!confirmed) return;

    this.disabling2fa.set(true);
    this.api.disable2fa(this.twoFaPassword(), this.twoFaCode()).subscribe({
      next: () => {
        this.toast.success('Two-factor authentication disabled');
        this.twoFaPassword.set('');
        this.twoFaCode.set('');
        this.disabling2fa.set(false);
        this.accountResource.reload();
      },
      error: () => {
        this.toast.error('Failed to disable 2FA. Check your password and code.');
        this.disabling2fa.set(false);
      },
    });
  }

  // API key

  // Plex
  onPlexChanged(): void {
    this.accountResource.reload();
  }

  // OIDC
  async saveOidcConfig(): Promise<void> {
    const m = this.oidcModel();
    if (m.enabled && !m.authorizedSubject) {
      const confirmed = await this.confirmService.confirm({
        title: 'Enable OIDC without a linked account',
        message:
          'No OIDC account is linked. Anyone who can authenticate with your identity provider ' +
          'and has access to this application will be able to sign in as the administrator. ' +
          'This is intended for self-hosted providers (Authentik, Keycloak, Authelia) where ' +
          'you control every account. It is UNSAFE with public providers such as Google, ' +
          'Microsoft personal accounts, or Auth0 tenants with open registration. ' +
          'Click "Link Account" after saving to restrict access to a single identity.',
        confirmLabel: 'Enable anyway',
        destructive: true,
      });
      if (!confirmed) {
        return;
      }
    }

    this.oidcSaving.set(true);
    this.api.updateOidcConfig({
      enabled: m.enabled,
      issuerUrl: m.issuerUrl,
      clientId: m.clientId,
      clientSecret: m.clientSecret,
      scopes: m.scopes,
      authorizedSubject: m.authorizedSubject,
      providerName: m.providerName,
      redirectUrl: m.redirectUrl,
      exclusiveMode: m.exclusiveMode,
    }).subscribe({
      next: () => {
        this.toast.success('OIDC settings saved');
        this.oidcSaving.set(false);
        this.oidcSaved.set(true);
        setTimeout(() => this.oidcSaved.set(false), 1500);
      },
      error: () => {
        this.toast.error('Failed to save OIDC settings');
        this.oidcSaving.set(false);
      },
    });
  }

  startOidcLink(): void {
    this.oidcLinking.set(true);
    this.auth.startOidcLink().subscribe({
      next: (result) => {
        window.location.href = result.authorizationUrl;
      },
      error: () => {
        this.toast.error('Failed to start OIDC account linking');
        this.oidcLinking.set(false);
      },
    });
  }

  async confirmUnlinkOidc(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Unlink OIDC Account',
      message: 'This will remove the linked identity. Anyone who can authenticate with your identity provider and is allowed to access this application will be able to sign in.',
      confirmLabel: 'Unlink',
      destructive: true,
    });
    if (!confirmed) return;

    this.oidcUnlinking.set(true);
    this.api.unlinkOidc().subscribe({
      next: () => {
        this.oidcModel.update(m => ({ ...m, authorizedSubject: '', exclusiveMode: false }));
        this.toast.success('OIDC account unlinked');
        this.oidcUnlinking.set(false);
      },
      error: () => {
        this.toast.error('Failed to unlink OIDC account');
        this.oidcUnlinking.set(false);
      },
    });
  }
}
