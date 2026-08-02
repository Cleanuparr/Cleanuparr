import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AccountApi } from '@core/api/account.api';
import { ConfirmOptions, ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { ApiKeyCardComponent } from './api-key-card.component';

describe('ApiKeyCardComponent', () => {
  afterEach(() => {
    Reflect.deleteProperty(navigator, 'clipboard');
  });

  function setup(options: {
    revealFails?: boolean;
    regenerateFails?: boolean;
    clipboardFails?: boolean;
    confirmAnswer?: boolean;
  } = {}) {
    const toasts: string[] = [];
    const confirmations: ConfirmOptions[] = [];
    const writeText = vi.fn(() =>
      options.clipboardFails ? Promise.reject(new Error('denied')) : Promise.resolve(),
    );
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    let regenerateCalls = 0;

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccountApi,
          useValue: {
            getApiKey: () =>
              options.revealFails ? throwError(() => new Error('boom')) : of({ apiKey: 'live-key-1234' }),
            regenerateApiKey: () => {
              regenerateCalls++;
              return options.regenerateFails
                ? throwError(() => new Error('boom'))
                : of({ apiKey: 'fresh-key-9999' });
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

    const fixture = TestBed.createComponent(ApiKeyCardComponent);
    fixture.componentRef.setInput('apiKeyPreview', 'live****1234');
    fixture.detectChanges();
    return { fixture, toasts, confirmations, writeText, regenerateCalls: () => regenerateCalls };
  }

  function shownKey(fixture: ComponentFixture<ApiKeyCardComponent>): string {
    return (fixture.nativeElement.querySelector('.api-key-value') as HTMLElement).textContent!.trim();
  }

  function buttonLabels(fixture: ComponentFixture<ApiKeyCardComponent>): string[] {
    return Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button.btn')).map((button) =>
      button.textContent!.trim(),
    );
  }

  function click(fixture: ComponentFixture<ApiKeyCardComponent>, label: string): void {
    const button = Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button.btn')).find(
      (candidate) => candidate.textContent!.trim() === label,
    );
    button!.click();
    fixture.detectChanges();
  }

  it('shows only the masked preview and no copy action before revealing', () => {
    const { fixture } = setup();

    expect(shownKey(fixture)).toBe('live****1234');
    expect(fixture.nativeElement.querySelector('.api-key-value--masked')).not.toBeNull();
    expect(buttonLabels(fixture)).toEqual(['Reveal', 'Regenerate API Key']);
  });

  it('fetches the key on reveal and clears it again on hide', () => {
    const { fixture } = setup();

    click(fixture, 'Reveal');

    expect(shownKey(fixture)).toBe('live-key-1234');
    expect(fixture.nativeElement.querySelector('.api-key-value--masked')).toBeNull();
    expect(buttonLabels(fixture)).toEqual(['Hide', 'Copy', 'Regenerate API Key']);

    click(fixture, 'Hide');

    expect(shownKey(fixture)).toBe('live****1234');
    expect(fixture.componentInstance.apiKey()).toBe('');
    expect(buttonLabels(fixture)).toEqual(['Reveal', 'Regenerate API Key']);
  });

  it('keeps the key masked and reports the failure when it cannot be loaded', () => {
    const { fixture, toasts } = setup({ revealFails: true });

    click(fixture, 'Reveal');

    expect(fixture.componentInstance.apiKeyRevealed()).toBe(false);
    expect(shownKey(fixture)).toBe('live****1234');
    expect(toasts).toEqual(['error:Failed to load API key']);
  });

  it('copies the revealed key to the clipboard', async () => {
    const { fixture, toasts, writeText } = setup();

    click(fixture, 'Reveal');
    click(fixture, 'Copy');
    await Promise.resolve();

    expect(writeText).toHaveBeenCalledWith('live-key-1234');
    expect(toasts).toEqual(['success:API key copied to clipboard']);
  });

  it('reports a rejected clipboard write', async () => {
    const { fixture, toasts } = setup({ clipboardFails: true });

    click(fixture, 'Reveal');
    click(fixture, 'Copy');
    await Promise.resolve();

    expect(toasts).toEqual(['error:Failed to copy API key']);
  });

  it('does not regenerate when the destructive confirmation is declined', async () => {
    const { fixture, confirmations, regenerateCalls } = setup({ confirmAnswer: false });

    click(fixture, 'Reveal');
    await fixture.componentInstance.confirmRegenerateApiKey();
    fixture.detectChanges();

    expect(confirmations).toEqual([
      {
        title: 'Regenerate API Key',
        message: 'This will invalidate the current API key. Any integrations using this key will stop working.',
        confirmLabel: 'Regenerate',
        destructive: true,
      },
    ]);
    expect(regenerateCalls()).toBe(0);
    expect(shownKey(fixture)).toBe('live-key-1234');
  });

  it('replaces the old key with the regenerated one and reveals it', async () => {
    const { fixture, toasts, regenerateCalls } = setup();

    await fixture.componentInstance.confirmRegenerateApiKey();
    fixture.detectChanges();

    expect(regenerateCalls()).toBe(1);
    expect(shownKey(fixture)).toBe('fresh-key-9999');
    expect(fixture.componentInstance.apiKeyRevealed()).toBe(true);
    expect(fixture.componentInstance.regeneratingApiKey()).toBe(false);
    expect(toasts).toEqual(['success:API key regenerated']);
  });

  it('reports a failed regeneration and keeps the previous key', async () => {
    const { fixture, toasts } = setup({ regenerateFails: true });

    click(fixture, 'Reveal');
    await fixture.componentInstance.confirmRegenerateApiKey();
    fixture.detectChanges();

    expect(shownKey(fixture)).toBe('live-key-1234');
    expect(fixture.componentInstance.regeneratingApiKey()).toBe(false);
    expect(toasts).toEqual(['error:Failed to regenerate API key']);
  });
});
