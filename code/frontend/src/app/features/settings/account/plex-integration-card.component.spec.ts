import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { AccountApi, PlexPinResponse, PlexPinStatus } from '@core/api/account.api';
import { ConfirmOptions, ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { PlexIntegrationCardComponent } from './plex-integration-card.component';

@Component({
  imports: [PlexIntegrationCardComponent],
  template: `<app-plex-integration-card
    [linked]="linked()"
    [username]="username()"
    [oidcExclusiveMode]="exclusive()"
    (changed)="changes.push('changed')"
  />`,
})
class HostComponent {
  readonly linked = signal(false);
  readonly username = signal('');
  readonly exclusive = signal(false);
  readonly changes: string[] = [];
}

const PIN: PlexPinResponse = { pinId: 4242, authUrl: 'https://plex.tv/link?code=ABCD' };

describe('PlexIntegrationCardComponent', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  function setup(options: {
    linked?: boolean;
    linkFails?: boolean;
    popupBlocked?: boolean;
    verify?: () => Observable<PlexPinStatus>;
    unlinkFails?: boolean;
    confirmAnswer?: boolean;
  } = {}) {
    const toasts: string[] = [];
    const confirmations: ConfirmOptions[] = [];
    const verifiedPins: number[] = [];
    const authWindow = { location: { href: '' }, close: vi.fn() };
    const opened: string[] = [];
    const open = vi.fn((url: string) => {
      opened.push(url);
      return options.popupBlocked ? null : authWindow;
    });
    vi.stubGlobal('open', open);

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccountApi,
          useValue: {
            linkPlex: () => (options.linkFails ? throwError(() => new Error('boom')) : of(PIN)),
            verifyPlexLink: (pinId: number) => {
              verifiedPins.push(pinId);
              return options.verify ? options.verify() : of({ completed: false } as PlexPinStatus);
            },
            unlinkPlex: () => (options.unlinkFails ? throwError(() => new Error('boom')) : of(undefined)),
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
    fixture.componentInstance.linked.set(options.linked ?? false);
    fixture.detectChanges();
    return { fixture, toasts, confirmations, verifiedPins, authWindow, opened, open };
  }

  function card(fixture: ComponentFixture<HostComponent>): PlexIntegrationCardComponent {
    return fixture.debugElement.children[0].componentInstance as PlexIntegrationCardComponent;
  }

  function actionButton(fixture: ComponentFixture<HostComponent>): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button.btn') as HTMLButtonElement;
  }

  function click(fixture: ComponentFixture<HostComponent>): void {
    actionButton(fixture).click();
    fixture.detectChanges();
  }

  it('opens the popup up front and sends it to the Plex auth url once the pin arrives', () => {
    const { fixture, authWindow, opened } = setup();

    click(fixture);

    expect(opened).toEqual(['']);
    expect(authWindow.location.href).toBe(PIN.authUrl);
    expect(card(fixture).plexLinking()).toBe(true);
    expect(actionButton(fixture).textContent!.trim()).toBe('Waiting for Plex...');
    expect(actionButton(fixture).disabled).toBe(true);
  });

  it('opens the auth url in a fresh window when the popup was blocked', () => {
    const { fixture, opened } = setup({ popupBlocked: true });

    click(fixture);

    expect(opened).toEqual(['', PIN.authUrl]);
  });

  it('closes the popup and reports a failed link start', () => {
    const { fixture, toasts, authWindow, verifiedPins } = setup({ linkFails: true });

    click(fixture);
    vi.advanceTimersByTime(10000);

    expect(authWindow.close).toHaveBeenCalledTimes(1);
    expect(toasts).toEqual(['error:Failed to start Plex linking']);
    expect(card(fixture).plexLinking()).toBe(false);
    expect(verifiedPins).toEqual([]);
  });

  it('polls the pin every two seconds and links the account once it completes', () => {
    let completed = false;
    const { fixture, toasts, verifiedPins } = setup({ verify: () => of({ completed, plexUsername: 'ziggy' }) });

    click(fixture);
    vi.advanceTimersByTime(4000);

    expect(verifiedPins).toEqual([4242, 4242]);
    expect(fixture.componentInstance.changes).toEqual([]);

    completed = true;
    vi.advanceTimersByTime(2000);
    fixture.detectChanges();

    expect(toasts).toEqual(['success:Plex account linked']);
    expect(fixture.componentInstance.changes).toEqual(['changed']);
    expect(card(fixture).plexLinking()).toBe(false);

    vi.advanceTimersByTime(10000);

    expect(verifiedPins).toHaveLength(3);
  });

  it('surfaces a verify failure and stops polling', () => {
    const { fixture, toasts, verifiedPins } = setup({ verify: () => throwError(() => new Error('plex down')) });

    click(fixture);
    vi.advanceTimersByTime(10000);
    fixture.detectChanges();

    expect(verifiedPins).toEqual([4242]);
    expect(toasts).toEqual(['error:Plex linking failed']);
    expect(card(fixture).plexLinking()).toBe(false);
    expect(fixture.componentInstance.changes).toEqual([]);
  });

  it('times out after the poller gives up', () => {
    const { fixture, toasts, verifiedPins } = setup();

    click(fixture);
    vi.advanceTimersByTime(120000);

    expect(verifiedPins).toHaveLength(60);
    expect(toasts).toEqual([]);

    vi.advanceTimersByTime(2000);
    fixture.detectChanges();

    expect(toasts).toEqual(['error:Plex linking timed out']);
    expect(card(fixture).plexLinking()).toBe(false);
  });

  it('stops polling when the card is destroyed', () => {
    const { fixture, verifiedPins } = setup();

    click(fixture);
    vi.advanceTimersByTime(4000);

    expect(verifiedPins).toHaveLength(2);

    fixture.destroy();
    vi.advanceTimersByTime(20000);

    expect(verifiedPins).toHaveLength(2);
  });

  it('unlinks only after the destructive confirmation is accepted', async () => {
    const { fixture, toasts, confirmations } = setup({ linked: true, confirmAnswer: false });
    fixture.componentInstance.username.set('ziggy');
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('.status-value') as HTMLElement).textContent!.trim()).toBe('ziggy');

    await card(fixture).confirmUnlinkPlex();
    fixture.detectChanges();

    expect(confirmations).toEqual([
      {
        title: 'Unlink Plex',
        message: 'This will remove your linked Plex account. You will no longer be able to log in with Plex.',
        confirmLabel: 'Unlink',
        destructive: true,
      },
    ]);
    expect(toasts).toEqual([]);
    expect(fixture.componentInstance.changes).toEqual([]);
  });

  it('reports the unlink and asks the parent to reload', async () => {
    const { fixture, toasts } = setup({ linked: true });

    await card(fixture).confirmUnlinkPlex();
    fixture.detectChanges();

    expect(toasts).toEqual(['success:Plex account unlinked']);
    expect(fixture.componentInstance.changes).toEqual(['changed']);
    expect(card(fixture).plexUnlinking()).toBe(false);
  });

  it('disables both actions under OIDC exclusive mode', () => {
    const { fixture } = setup();
    fixture.componentInstance.exclusive.set(true);
    fixture.detectChanges();

    expect(actionButton(fixture).disabled).toBe(true);
    expect((fixture.nativeElement.querySelector('.section-notice') as HTMLElement).textContent!.trim()).toBe(
      'Plex login is disabled while OIDC exclusive mode is active.',
    );

    fixture.componentInstance.linked.set(true);
    fixture.detectChanges();

    expect(actionButton(fixture).disabled).toBe(true);
  });
});
