import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AccountApi, AccountInfo } from '@core/api/account.api';
import { AuthService } from '@core/auth/auth.service';
import { ConfirmOptions, ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { OidcConfig } from '@shared/models/oidc-config.model';
import { AccountSettingsComponent } from './account-settings.component';
import { TwoFactorCardComponent } from './two-factor-card.component';

const ACCOUNT: AccountInfo = {
  username: 'admin',
  plexLinked: true,
  plexUsername: 'ziggy',
  twoFactorEnabled: true,
  apiKeyPreview: 'live****1234',
};

const OIDC: OidcConfig = {
  enabled: false,
  issuerUrl: '',
  clientId: '',
  clientSecret: '',
  scopes: '',
  authorizedSubject: '',
  providerName: '',
  redirectUrl: '',
  exclusiveMode: false,
};

const ENABLED_OIDC: OidcConfig = {
  enabled: true,
  issuerUrl: 'https://auth.example.com/',
  clientId: 'cleanuparr',
  clientSecret: 'shhh',
  scopes: 'openid email',
  authorizedSubject: 'subject-123',
  providerName: 'Authentik',
  redirectUrl: 'https://cleanuparr.example.com',
  exclusiveMode: true,
};

describe('AccountSettingsComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function setup(options: {
    oidc?: OidcConfig;
    loadFails?: boolean;
    saveFails?: boolean;
    unlinkFails?: boolean;
    confirmAnswer?: boolean;
    queryParams?: Record<string, string>;
  } = {}) {
    const toasts: string[] = [];
    const confirmations: ConfirmOptions[] = [];
    const savedConfigs: Partial<OidcConfig>[] = [];
    let infoCalls = 0;
    let unlinkCalls = 0;

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParams: options.queryParams ?? {} } },
        },
        {
          provide: AccountApi,
          useValue: {
            getInfo: (): Observable<AccountInfo> => {
              infoCalls++;
              return options.loadFails ? throwError(() => new Error('boom')) : of(ACCOUNT);
            },
            getOidcConfig: () => of(options.oidc ?? OIDC),
            updateOidcConfig: (config: Partial<OidcConfig>) => {
              savedConfigs.push(config);
              return options.saveFails ? throwError(() => new Error('boom')) : of(undefined);
            },
            unlinkOidc: () => {
              unlinkCalls++;
              return options.unlinkFails ? throwError(() => new Error('boom')) : of(undefined);
            },
            getApiKey: () => of({ apiKey: 'live-key-1234' }),
            regenerateApiKey: () => of({ apiKey: 'fresh-key-9999' }),
            changePassword: () => of(undefined),
            enable2fa: () => of({ secret: 's', qrCodeUri: 'otpauth://x', recoveryCodes: [] }),
            verifyEnable2fa: () => of(undefined),
            disable2fa: () => of(undefined),
            regenerate2fa: () => of({ secret: 's', qrCodeUri: 'otpauth://x', recoveryCodes: [] }),
            linkPlex: () => of({ pinId: 1, authUrl: 'https://plex.tv/link' }),
            verifyPlexLink: () => of({ completed: false }),
            unlinkPlex: () => of(undefined),
          },
        },
        {
          provide: AuthService,
          useValue: { startOidcLink: () => throwError(() => new Error('boom')) },
        },
        {
          provide: ToastService,
          useValue: {
            success: (message: string) => toasts.push(`success:${message}`),
            error: (message: string) => toasts.push(`error:${message}`),
          },
        },
        {
          provide: ConfirmService,
          useValue: {
            confirm: (confirmOptions: ConfirmOptions) => {
              confirmations.push(confirmOptions);
              return Promise.resolve(options.confirmAnswer ?? true);
            },
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(AccountSettingsComponent);
    fixture.detectChanges();
    return {
      fixture,
      toasts,
      confirmations,
      savedConfigs,
      infoCalls: () => infoCalls,
      unlinkCalls: () => unlinkCalls,
    };
  }

  function cardTitles(fixture: ComponentFixture<AccountSettingsComponent>): string[] {
    return Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('.card__title')).map((title) =>
      title.textContent!.trim(),
    );
  }

  function labelledInput(
    fixture: ComponentFixture<AccountSettingsComponent>,
    label: string,
  ): HTMLInputElement | null {
    const wrapper = Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('.input-label')).find((element) =>
      element.textContent!.trim().startsWith(label),
    );
    if (!wrapper) {
      return null;
    }
    return fixture.nativeElement.querySelector(`#${wrapper.getAttribute('for')}`) as HTMLInputElement;
  }

  function button(fixture: ComponentFixture<AccountSettingsComponent>, label: string): HTMLButtonElement {
    return Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button.btn')).find(
      (candidate) => candidate.textContent!.trim() === label,
    )!;
  }

  function toggle(fixture: ComponentFixture<AccountSettingsComponent>, label: string): HTMLButtonElement {
    return Array.from<HTMLButtonElement>(
      fixture.nativeElement.querySelectorAll('button.toggle__track'),
    ).find((candidate) => candidate.getAttribute('aria-label') === label)!;
  }

  it('renders every account card once the resource resolves', () => {
    const { fixture, toasts } = setup();

    expect(cardTitles(fixture)).toEqual([
      'Change Password',
      'Two-Factor Authentication',
      'API Key',
      'Plex Integration',
      'OIDC / SSO',
    ]);
    expect(fixture.componentInstance.account()).toEqual(ACCOUNT);
    expect(fixture.nativeElement.querySelector('.api-key-value')!.textContent!.trim()).toBe('live****1234');
    expect(fixture.nativeElement.querySelector('.status-value--active')!.textContent!.trim()).toBe('Active');
    expect(fixture.nativeElement.textContent).toContain('ziggy');
    expect(toasts).toEqual([]);
  });

  it('shows the retry empty state when the account cannot be loaded', () => {
    const { fixture, toasts, infoCalls } = setup({ loadFails: true });

    expect(fixture.componentInstance.loadError()).toBe(true);
    expect(fixture.nativeElement.querySelector('.empty-state__heading')!.textContent!.trim()).toBe(
      'Could not connect to server',
    );
    expect(cardTitles(fixture)).toEqual([]);
    expect(toasts).toEqual(['error:Failed to load account information']);
    expect(infoCalls()).toBe(1);

    button(fixture, 'Retry').click();
    fixture.detectChanges();

    expect(infoCalls()).toBe(2);
  });

  it('reloads the account when a card reports a change', () => {
    const { fixture, infoCalls } = setup();

    expect(infoCalls()).toBe(1);

    const card = fixture.debugElement.query(By.directive(TwoFactorCardComponent));
    (card.componentInstance as TwoFactorCardComponent).changed.emit();
    fixture.detectChanges();

    expect(infoCalls()).toBe(2);
  });

  it('hides the OIDC fields until OIDC is switched on', () => {
    const { fixture } = setup();

    expect(labelledInput(fixture, 'Issuer URL')).toBeNull();

    toggle(fixture, 'Enable OIDC').click();
    fixture.detectChanges();

    expect(labelledInput(fixture, 'Issuer URL')!.value).toBe('');
    expect(fixture.nativeElement.textContent).toContain('No account linked');
    expect(button(fixture, 'Link Account')).toBeDefined();
    expect(button(fixture, 'Save OIDC Settings').disabled).toBe(true);
  });

  it('fills the form from the loaded config and shows the linked subject', async () => {
    const { fixture } = setup({ oidc: ENABLED_OIDC });
    await fixture.whenStable();

    expect(labelledInput(fixture, 'Issuer URL')!.value).toBe(ENABLED_OIDC.issuerUrl);
    expect(labelledInput(fixture, 'Client ID')!.value).toBe(ENABLED_OIDC.clientId);
    expect(labelledInput(fixture, 'Provider Name')!.value).toBe(ENABLED_OIDC.providerName);
    expect(fixture.nativeElement.querySelector('.oidc-link-section__subject')!.textContent!.trim()).toBe(
      'subject-123',
    );
    expect(toggle(fixture, 'Exclusive Mode').getAttribute('aria-checked')).toBe('true');
    expect(button(fixture, 'Save OIDC Settings').disabled).toBe(false);
  });

  it('saves the config and shows the confirmation label until it expires', () => {
    vi.useFakeTimers();
    const { fixture, toasts, savedConfigs } = setup({ oidc: ENABLED_OIDC });

    button(fixture, 'Save OIDC Settings').click();
    fixture.detectChanges();

    expect(savedConfigs).toEqual([
      {
        enabled: true,
        issuerUrl: 'https://auth.example.com/',
        clientId: 'cleanuparr',
        clientSecret: 'shhh',
        scopes: 'openid email',
        authorizedSubject: 'subject-123',
        providerName: 'Authentik',
        redirectUrl: 'https://cleanuparr.example.com',
        exclusiveMode: true,
      },
    ]);
    expect(toasts).toEqual(['success:OIDC settings saved']);
    expect(button(fixture, 'Saved!').disabled).toBe(true);

    vi.advanceTimersByTime(1500);
    fixture.detectChanges();

    expect(fixture.componentInstance.oidcSaved()).toBe(false);
    expect(button(fixture, 'Save OIDC Settings').disabled).toBe(false);
  });

  it('warns before enabling OIDC with no linked account and aborts when declined', async () => {
    const { fixture, savedConfigs, confirmations } = setup({ confirmAnswer: false });

    toggle(fixture, 'Enable OIDC').click();
    fixture.detectChanges();
    await fixture.componentInstance.saveOidcConfig();
    fixture.detectChanges();

    expect(confirmations).toHaveLength(1);
    expect(confirmations[0]).toMatchObject({
      title: 'Enable OIDC without a linked account',
      confirmLabel: 'Enable anyway',
      destructive: true,
    });
    expect(savedConfigs).toEqual([]);
  });

  it('saves without prompting once an account is linked', async () => {
    const { fixture, savedConfigs, confirmations } = setup({ oidc: ENABLED_OIDC });

    await fixture.componentInstance.saveOidcConfig();
    fixture.detectChanges();

    expect(confirmations).toEqual([]);
    expect(savedConfigs).toHaveLength(1);
  });

  it('reports a rejected save', async () => {
    const { fixture, toasts } = setup({ oidc: ENABLED_OIDC, saveFails: true });

    await fixture.componentInstance.saveOidcConfig();
    fixture.detectChanges();

    expect(toasts).toEqual(['error:Failed to save OIDC settings']);
    expect(fixture.componentInstance.oidcSaving()).toBe(false);
    expect(fixture.componentInstance.oidcSaved()).toBe(false);
  });

  it('clears the subject and exclusive mode after the unlink is confirmed', async () => {
    const { fixture, toasts, unlinkCalls } = setup({ oidc: ENABLED_OIDC });

    await fixture.componentInstance.confirmUnlinkOidc();
    fixture.detectChanges();

    expect(unlinkCalls()).toBe(1);
    expect(toasts).toEqual(['success:OIDC account unlinked']);
    expect(fixture.componentInstance.oidcAuthorizedSubject()).toBe('');
    expect(fixture.componentInstance.oidcExclusiveMode()).toBe(false);
    expect(fixture.nativeElement.querySelector('.oidc-link-section__subject')).toBeNull();
    expect(button(fixture, 'Unlink')).toBeUndefined();
  });

  it('keeps the link when the unlink confirmation is declined', async () => {
    const { fixture, unlinkCalls } = setup({ oidc: ENABLED_OIDC, confirmAnswer: false });

    await fixture.componentInstance.confirmUnlinkOidc();
    fixture.detectChanges();

    expect(unlinkCalls()).toBe(0);
    expect(fixture.componentInstance.oidcAuthorizedSubject()).toBe('subject-123');
  });

  it('drops exclusive mode when OIDC is switched off', () => {
    const { fixture } = setup({ oidc: ENABLED_OIDC });

    expect(fixture.componentInstance.oidcExclusiveMode()).toBe(true);

    toggle(fixture, 'Enable OIDC').click();
    fixture.detectChanges();

    expect(fixture.componentInstance.oidcExclusiveMode()).toBe(false);
    expect(labelledInput(fixture, 'Issuer URL')).toBeNull();
  });

  it('expands the OIDC section and reports the outcome of a link redirect', () => {
    const { fixture, toasts } = setup({ queryParams: { oidc_link: 'success' } });

    expect(toasts).toEqual(['success:OIDC account linked successfully']);
    expect(fixture.componentInstance.oidcExpanded()).toBe(true);
  });

  it('reports a failed start of the OIDC link flow', () => {
    const { fixture, toasts } = setup({ oidc: ENABLED_OIDC });

    button(fixture, 'Re-link').click();
    fixture.detectChanges();

    expect(toasts).toEqual(['error:Failed to start OIDC account linking']);
    expect(fixture.componentInstance.oidcLinking()).toBe(false);
  });
});
