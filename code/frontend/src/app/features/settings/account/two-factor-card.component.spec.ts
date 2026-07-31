import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AccountApi, Regenerate2faRequest, TotpSetupResponse } from '@core/api/account.api';
import { ConfirmOptions, ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { TwoFactorCardComponent } from './two-factor-card.component';

@Component({
  imports: [TwoFactorCardComponent],
  template: `<app-two-factor-card [enabled]="enabled()" (changed)="changes.push('changed')" />`,
})
class HostComponent {
  readonly enabled = signal(false);
  readonly changes: string[] = [];
}

const SETUP: TotpSetupResponse = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUri: 'otpauth://totp/Cleanuparr:admin?secret=JBSWY3DPEHPK3PXP',
  recoveryCodes: ['aaaa-1111', 'bbbb-2222', 'cccc-3333'],
};

const REGENERATED: TotpSetupResponse = {
  secret: 'NEWSECRET1234567',
  qrCodeUri: 'otpauth://totp/Cleanuparr:admin?secret=NEWSECRET1234567',
  recoveryCodes: ['dddd-4444', 'eeee-5555'],
};

describe('TwoFactorCardComponent', () => {
  function setup(options: {
    enabled?: boolean;
    enableFails?: boolean;
    verifyFails?: boolean;
    disableFails?: boolean;
    regenerateFails?: boolean;
    confirmAnswer?: boolean;
  } = {}) {
    const toasts: string[] = [];
    const confirmations: ConfirmOptions[] = [];
    const enablePasswords: string[] = [];
    const verifiedCodes: string[] = [];
    const disableCalls: [string, string][] = [];
    const regenerateCalls: Regenerate2faRequest[] = [];

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccountApi,
          useValue: {
            enable2fa: (password: string) => {
              enablePasswords.push(password);
              return options.enableFails ? throwError(() => new Error('boom')) : of(SETUP);
            },
            verifyEnable2fa: (code: string) => {
              verifiedCodes.push(code);
              return options.verifyFails ? throwError(() => new Error('boom')) : of(undefined);
            },
            disable2fa: (password: string, totpCode: string) => {
              disableCalls.push([password, totpCode]);
              return options.disableFails ? throwError(() => new Error('boom')) : of(undefined);
            },
            regenerate2fa: (request: Regenerate2faRequest) => {
              regenerateCalls.push(request);
              return options.regenerateFails ? throwError(() => new Error('boom')) : of(REGENERATED);
            },
          },
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

    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.enabled.set(options.enabled ?? false);
    fixture.detectChanges();
    return { fixture, toasts, confirmations, enablePasswords, verifiedCodes, disableCalls, regenerateCalls };
  }

  function card(fixture: ComponentFixture<HostComponent>): TwoFactorCardComponent {
    return fixture.debugElement.children[0].componentInstance as TwoFactorCardComponent;
  }

  function type(fixture: ComponentFixture<HostComponent>, placeholder: string, value: string): void {
    const input = fixture.nativeElement.querySelector(`input[placeholder="${placeholder}"]`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function button(fixture: ComponentFixture<HostComponent>, label: string): HTMLButtonElement {
    return Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button.btn')).find(
      (candidate) => candidate.textContent!.trim() === label,
    )!;
  }

  function click(fixture: ComponentFixture<HostComponent>, label: string): void {
    button(fixture, label).click();
    fixture.detectChanges();
  }

  function secret(fixture: ComponentFixture<HostComponent>): string | null {
    const element = fixture.nativeElement.querySelector('.qr-secret') as HTMLElement | null;
    return element ? element.textContent!.trim() : null;
  }

  function recoveryCodes(fixture: ComponentFixture<HostComponent>): string[] {
    return Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('.recovery-code')).map((code) =>
      code.textContent!.trim(),
    );
  }

  it('shows the disabled status and no secret until setup is started', () => {
    const { fixture } = setup();

    expect((fixture.nativeElement.querySelector('.status-value') as HTMLElement).textContent!.trim()).toBe('Disabled');
    expect(secret(fixture)).toBeNull();
    expect(recoveryCodes(fixture)).toEqual([]);
    expect(button(fixture, 'Enable 2FA').disabled).toBe(true);
  });

  it('reveals the qr secret and recovery codes only after the setup call succeeds', () => {
    const { fixture, enablePasswords } = setup();

    type(fixture, 'Enter your password to enable 2FA', 'my-password');

    expect(button(fixture, 'Enable 2FA').disabled).toBe(false);

    click(fixture, 'Enable 2FA');

    expect(enablePasswords).toEqual(['my-password']);
    expect(card(fixture).enableSetup()).toBe(true);
    expect(secret(fixture)).toBe(SETUP.secret);
    expect(recoveryCodes(fixture)).toEqual(SETUP.recoveryCodes);
    expect(fixture.componentInstance.changes).toEqual([]);
  });

  it('keeps the secret hidden and reports a failed setup start', () => {
    const { fixture, toasts } = setup({ enableFails: true });

    type(fixture, 'Enter your password to enable 2FA', 'wrong-password');
    click(fixture, 'Enable 2FA');

    expect(card(fixture).enableSetup()).toBe(false);
    expect(secret(fixture)).toBeNull();
    expect(toasts).toEqual(['error:Failed to start 2FA setup. Check your password.']);
  });

  it('rejects an invalid verification code without enabling 2FA', () => {
    const { fixture, toasts, verifiedCodes } = setup({ verifyFails: true });

    type(fixture, 'Enter your password to enable 2FA', 'my-password');
    click(fixture, 'Enable 2FA');
    type(fixture, 'Enter 6-digit code from your app', '00000');

    expect(button(fixture, 'Verify & Enable 2FA').disabled).toBe(true);

    type(fixture, 'Enter 6-digit code from your app', '000000');
    click(fixture, 'Verify & Enable 2FA');

    expect(verifiedCodes).toEqual(['000000']);
    expect(toasts).toEqual(['error:Invalid verification code']);
    expect(card(fixture).enableSetup()).toBe(true);
    expect(secret(fixture)).toBe(SETUP.secret);
    expect(fixture.componentInstance.changes).toEqual([]);
  });

  it('enables 2FA on a valid code, clears the setup state and notifies the parent', () => {
    const { fixture, toasts, verifiedCodes } = setup();

    type(fixture, 'Enter your password to enable 2FA', 'my-password');
    click(fixture, 'Enable 2FA');
    type(fixture, 'Enter 6-digit code from your app', '123456');
    click(fixture, 'Verify & Enable 2FA');

    expect(verifiedCodes).toEqual(['123456']);
    expect(toasts).toEqual(['success:Two-factor authentication enabled']);
    expect(fixture.componentInstance.changes).toEqual(['changed']);
    expect(card(fixture).enableSetup()).toBe(false);
    expect(card(fixture).newRecoveryCodes()).toEqual([]);
    expect(card(fixture).enableVerificationCode()).toBe('');
    expect(secret(fixture)).toBeNull();
  });

  it('discards the pending setup when it is cancelled', () => {
    const { fixture } = setup();

    type(fixture, 'Enter your password to enable 2FA', 'my-password');
    click(fixture, 'Enable 2FA');
    click(fixture, 'Cancel');

    expect(card(fixture).enableSetup()).toBe(false);
    expect(card(fixture).enablePassword()).toBe('');
    expect(card(fixture).newTotpSecret()).toBe('');
    expect(secret(fixture)).toBeNull();
  });

  it('requires a password and a six digit code before offering to disable or regenerate', () => {
    const { fixture } = setup({ enabled: true });

    expect((fixture.nativeElement.querySelector('.status-value') as HTMLElement).textContent!.trim()).toBe('Active');
    expect(button(fixture, 'Disable 2FA').disabled).toBe(true);
    expect(button(fixture, 'Regenerate 2FA').disabled).toBe(true);

    type(fixture, 'Enter your password', 'my-password');
    type(fixture, 'Enter 6-digit code', '12345');

    expect(button(fixture, 'Disable 2FA').disabled).toBe(true);

    type(fixture, 'Enter 6-digit code', '123456');

    expect(button(fixture, 'Disable 2FA').disabled).toBe(false);
    expect(button(fixture, 'Regenerate 2FA').disabled).toBe(false);
  });

  it('does not disable 2FA when the confirmation is declined', async () => {
    const { fixture, confirmations, disableCalls } = setup({ enabled: true, confirmAnswer: false });

    type(fixture, 'Enter your password', 'my-password');
    type(fixture, 'Enter 6-digit code', '123456');
    await card(fixture).confirmDisable2fa();
    fixture.detectChanges();

    expect(confirmations).toEqual([
      {
        title: 'Disable 2FA',
        message: 'This will remove two-factor authentication from your account. Your recovery codes will be deleted.',
        confirmLabel: 'Disable',
        destructive: true,
      },
    ]);
    expect(disableCalls).toEqual([]);
    expect(fixture.componentInstance.changes).toEqual([]);
  });

  it('disables 2FA after confirmation, clearing the credentials and notifying the parent', async () => {
    const { fixture, toasts, disableCalls } = setup({ enabled: true });

    type(fixture, 'Enter your password', 'my-password');
    type(fixture, 'Enter 6-digit code', '123456');
    await card(fixture).confirmDisable2fa();
    fixture.detectChanges();

    expect(disableCalls).toEqual([['my-password', '123456']]);
    expect(toasts).toEqual(['success:Two-factor authentication disabled']);
    expect(fixture.componentInstance.changes).toEqual(['changed']);
    expect(card(fixture).twoFaPassword()).toBe('');
    expect(card(fixture).twoFaCode()).toBe('');
  });

  it('shows the new secret and recovery codes once after a regeneration', async () => {
    const { fixture, toasts, regenerateCalls } = setup({ enabled: true });

    type(fixture, 'Enter your password', 'my-password');
    type(fixture, 'Enter 6-digit code', '123456');
    await card(fixture).confirmRegenerate2fa();
    fixture.detectChanges();

    expect(regenerateCalls).toEqual([{ password: 'my-password', totpCode: '123456' }]);
    expect(toasts).toEqual(['success:2FA regenerated. Scan the QR code and save your recovery codes!']);
    expect(secret(fixture)).toBe(REGENERATED.secret);
    expect(recoveryCodes(fixture)).toEqual(REGENERATED.recoveryCodes);
    expect(card(fixture).twoFaPassword()).toBe('');
    expect(fixture.componentInstance.changes).toEqual([]);

    click(fixture, 'Dismiss');

    expect(secret(fixture)).toBeNull();
    expect(recoveryCodes(fixture)).toEqual([]);
  });

  it('reports a rejected regeneration and shows no new codes', async () => {
    const { fixture, toasts } = setup({ enabled: true, regenerateFails: true });

    type(fixture, 'Enter your password', 'my-password');
    type(fixture, 'Enter 6-digit code', '123456');
    await card(fixture).confirmRegenerate2fa();
    fixture.detectChanges();

    expect(toasts).toEqual(['error:Failed to regenerate 2FA. Check your password and code.']);
    expect(secret(fixture)).toBeNull();
    expect(recoveryCodes(fixture)).toEqual([]);
    expect(card(fixture).twoFaPassword()).toBe('my-password');
  });
});
