import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { NotificationApi } from '@core/api/notification.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { NotificationProviderDto } from '@shared/models/notification-provider.model';
import { NotificationProviderType } from '@shared/models/enums';
import { By } from '@angular/platform-browser';
import { NotificationsComponent } from './notifications.component';
import { NotificationProviderModalComponent } from './notification-provider-modal.component';

const DISCORD_PROVIDER: NotificationProviderDto = {
  id: 'discord-1',
  name: 'Discord alerts',
  type: NotificationProviderType.Discord,
  isEnabled: true,
  events: {
    onFailedImportStrike: true,
    onStalledStrike: false,
    onSlowStrike: false,
    onQueueItemDeleted: true,
    onDownloadCleaned: false,
    onCategoryChanged: false,
    onSearchTriggered: false,
    onSearchItemGrabbed: false,
  },
  configuration: { webhookUrl: 'https://discord.com/api/webhooks/abc' },
};

const TELEGRAM_PROVIDER: NotificationProviderDto = {
  id: 'telegram-1',
  name: 'Telegram alerts',
  type: NotificationProviderType.Telegram,
  isEnabled: false,
  events: {
    onFailedImportStrike: false,
    onStalledStrike: false,
    onSlowStrike: true,
    onQueueItemDeleted: false,
    onDownloadCleaned: false,
    onCategoryChanged: false,
    onSearchTriggered: false,
    onSearchItemGrabbed: false,
  },
  configuration: { botToken: 'token', chatId: '-100' },
};

function createApi(providers: NotificationProviderDto[]) {
  return {
    getProviders: vi.fn(() => of({ providers })),
    deleteProvider: vi.fn(() => of(undefined)),
  };
}

function createToast() {
  return { success: vi.fn(), error: vi.fn() };
}

interface Setup {
  fixture: ComponentFixture<NotificationsComponent>;
  component: NotificationsComponent;
  api: ReturnType<typeof createApi>;
  toast: ReturnType<typeof createToast>;
  confirm: ConfirmService;
}

describe('NotificationsComponent', () => {
  beforeEach(() => {
    vi.stubGlobal('matchMedia', () => ({ matches: false }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  function setup(providers: NotificationProviderDto[] = [DISCORD_PROVIDER, TELEGRAM_PROVIDER]): Setup {
    const api = createApi(providers);
    const toast = createToast();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        { provide: NotificationApi, useValue: api },
        { provide: ToastService, useValue: toast },
      ],
    });

    const fixture = TestBed.createComponent(NotificationsComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      toast,
      confirm: TestBed.inject(ConfirmService),
    };
  }

  function root(fixture: ComponentFixture<NotificationsComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function texts(host: HTMLElement, selector: string): string[] {
    return Array.from(host.querySelectorAll(selector)).map((element) =>
      (element as HTMLElement).textContent!.trim(),
    );
  }

  function rowButton(
    fixture: ComponentFixture<NotificationsComponent>,
    rowIndex: number,
    label: string,
  ): HTMLButtonElement {
    const row = root(fixture).querySelectorAll('.provider-row')[rowIndex] as HTMLElement;
    return Array.from(row.querySelectorAll<HTMLButtonElement>('button')).find(
      (button) => button.textContent!.trim() === label,
    )!;
  }

  it('lists the loaded providers with their status and enabled events', () => {
    const { fixture, component } = setup();

    expect(component.providers()).toHaveLength(2);
    expect(texts(root(fixture), '.item-row__name')).toEqual(['Discord alerts', 'Telegram alerts']);
    expect(texts(root(fixture), '.provider-row app-badge')).toEqual([
      'Enabled',
      'Discord',
      'Disabled',
      'Telegram',
    ]);

    const rows = root(fixture).querySelectorAll<HTMLElement>('.provider-row__events');
    expect(texts(rows[0], '.event-tag')).toEqual(['Failed Import', 'Queue Deleted']);
    expect(texts(rows[1], '.event-tag')).toEqual(['Slow']);
  });

  it('shows the empty state when no providers are configured', () => {
    const { fixture } = setup([]);

    expect(root(fixture).querySelectorAll('.provider-row')).toHaveLength(0);
    expect(root(fixture).textContent).toContain('No notification providers');
  });

  it('opens the create modal for the type picked in the selection modal', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();
    expect(component.selectionModalVisible()).toBe(true);
    expect(texts(root(fixture), '.provider-card__name')).toEqual([
      'Apprise',
      'Discord',
      'Gotify',
      'Notifiarr',
      'ntfy',
      'Pushover',
      'Telegram',
    ]);

    const gotifyCard = Array.from(
      root(fixture).querySelectorAll<HTMLButtonElement>('.provider-card'),
    ).find((card) => card.textContent!.includes('Gotify'))!;
    gotifyCard.click();
    fixture.detectChanges();

    expect(component.selectionModalVisible()).toBe(false);
    expect(component.editingProvider()).toBeNull();
    expect(component.selectedType()).toBe(NotificationProviderType.Gotify);
    expect(component.modalVisible()).toBe(true);
    expect(texts(root(fixture), '.modal__title')).toContain('Add Gotify Provider');
  });

  it('opens the edit modal for the selected provider', () => {
    const { fixture, component } = setup();

    rowButton(fixture, 1, 'Edit').click();
    fixture.detectChanges();

    expect(component.editingProvider()).toBe(TELEGRAM_PROVIDER);
    expect(component.modalVisible()).toBe(true);
    expect(texts(root(fixture), '.modal__title')).toContain('Edit Telegram Provider');
  });

  it('deletes a provider only after the confirmation is accepted', async () => {
    const { fixture, component, api, toast, confirm } = setup();

    const cancelled = component.deleteProvider(DISCORD_PROVIDER);
    expect(confirm.state()).toMatchObject({
      title: 'Delete Provider',
      confirmLabel: 'Delete',
      destructive: true,
    });
    expect(confirm.state()!.message).toContain('Discord alerts');
    confirm.cancel();
    await cancelled;

    expect(api.deleteProvider).not.toHaveBeenCalled();
    expect(api.getProviders).toHaveBeenCalledTimes(1);

    const accepted = component.deleteProvider(DISCORD_PROVIDER);
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(api.deleteProvider).toHaveBeenCalledWith('discord-1');
    expect(toast.success).toHaveBeenCalledWith('Provider deleted');
    expect(api.getProviders).toHaveBeenCalledTimes(2);
  });

  it('toasts and keeps the list when the delete request fails', async () => {
    const { component, api, toast, confirm } = setup();

    api.deleteProvider.mockReturnValue(throwError(() => new Error('boom')));
    const pending = component.deleteProvider(TELEGRAM_PROVIDER);
    confirm.accept();
    await pending;

    expect(toast.error).toHaveBeenCalledWith('Failed to delete provider');
    expect(toast.success).not.toHaveBeenCalled();
    expect(api.getProviders).toHaveBeenCalledTimes(1);
  });

  it('shows the connection error state and reloads on retry', () => {
    const api = createApi([]);
    api.getProviders.mockReturnValue(throwError(() => new Error('offline')));
    const toast = createToast();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        { provide: NotificationApi, useValue: api },
        { provide: ToastService, useValue: toast },
      ],
    });

    const fixture = TestBed.createComponent(NotificationsComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.loadError()).toBe(true);
    expect(fixture.componentInstance.providers()).toEqual([]);
    expect(root(fixture).textContent).toContain('Could not connect to server');
    expect(toast.error).toHaveBeenCalledWith('Failed to load notification providers');

    api.getProviders.mockReturnValue(of({ providers: [DISCORD_PROVIDER] }));
    (root(fixture).querySelector('app-empty-state button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.loadError()).toBe(false);
    expect(texts(root(fixture), '.item-row__name')).toEqual(['Discord alerts']);
  });

  it('delegates pending changes to the provider modal', () => {
    const { fixture, component } = setup();

    expect(component.hasPendingChanges()).toBe(false);

    rowButton(fixture, 0, 'Edit').click();
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(false);

    const modal: NotificationProviderModalComponent = fixture.debugElement.query(
      By.directive(NotificationProviderModalComponent),
    ).componentInstance;
    modal.modalModel.update((m) => ({ ...m, name: 'Renamed' }));
    fixture.detectChanges();

    expect(component.hasPendingChanges()).toBe(true);
  });
});
