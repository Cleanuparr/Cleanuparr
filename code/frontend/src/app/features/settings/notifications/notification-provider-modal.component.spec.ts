import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { NotificationApi } from '@core/api/notification.api';
import { ToastService } from '@core/services/toast.service';
import { AppriseCliStatus, NotificationProviderDto } from '@shared/models/notification-provider.model';
import {
  NotificationProviderType,
  AppriseMode,
  NtfyAuthenticationType,
  NtfyPriority,
  PushoverPriority,
} from '@shared/models/enums';
import { NotificationProviderModalComponent } from './notification-provider-modal.component';

const DEFAULT_EVENTS = {
  onFailedImportStrike: true,
  onStalledStrike: true,
  onSlowStrike: true,
  onQueueItemDeleted: true,
  onDownloadCleaned: true,
  onCategoryChanged: false,
  onSearchTriggered: false,
  onSearchItemGrabbed: false,
};

const NTFY_PROVIDER: NotificationProviderDto = {
  id: 'ntfy-1',
  name: 'My ntfy',
  type: NotificationProviderType.Ntfy,
  isEnabled: false,
  events: {
    onFailedImportStrike: false,
    onStalledStrike: true,
    onSlowStrike: false,
    onQueueItemDeleted: true,
    onDownloadCleaned: false,
    onCategoryChanged: true,
    onSearchTriggered: false,
    onSearchItemGrabbed: true,
  },
  configuration: {
    serverUrl: 'https://ntfy.example.com',
    topics: ['alpha', 'beta'],
    authenticationType: NtfyAuthenticationType.BasicAuth,
    username: 'user',
    password: 'secret',
    priority: NtfyPriority.High,
    tags: ['warning'],
  },
};

const DISCORD_PROVIDER: NotificationProviderDto = {
  id: 'discord-1',
  name: 'My discord',
  type: NotificationProviderType.Discord,
  isEnabled: true,
  events: DEFAULT_EVENTS,
  configuration: {
    webhookUrl: 'https://discord.com/api/webhooks/abc',
    username: 'Cleanuparr',
  },
};

const GOTIFY_PROVIDER: NotificationProviderDto = {
  id: 'gotify-1',
  name: 'My gotify',
  type: NotificationProviderType.Gotify,
  isEnabled: true,
  events: DEFAULT_EVENTS,
  configuration: {
    serverUrl: 'https://gotify.example.com',
    applicationToken: 'token',
    priority: 8,
  },
};

function createApi(cliStatus: Observable<AppriseCliStatus> = of({ available: true, version: '1.9.0' })) {
  const created: NotificationProviderDto = DISCORD_PROVIDER;
  return {
    getAppriseCliStatus: vi.fn(() => cliStatus),
    createDiscord: vi.fn(() => of(created)),
    createTelegram: vi.fn(() => of(created)),
    createNotifiarr: vi.fn(() => of(created)),
    createApprise: vi.fn(() => of(created)),
    createNtfy: vi.fn(() => of(created)),
    createPushover: vi.fn(() => of(created)),
    createGotify: vi.fn(() => of(created)),
    updateDiscord: vi.fn(() => of(created)),
    updateTelegram: vi.fn(() => of(created)),
    updateNotifiarr: vi.fn(() => of(created)),
    updateApprise: vi.fn(() => of(created)),
    updateNtfy: vi.fn(() => of(created)),
    updatePushover: vi.fn(() => of(created)),
    updateGotify: vi.fn(() => of(created)),
    testDiscord: vi.fn(() => of({ message: 'Discord test sent' })),
    testTelegram: vi.fn(() => of({ message: 'Telegram test sent' })),
    testNotifiarr: vi.fn(() => of({ message: '' })),
    testApprise: vi.fn(() => of({ message: '' })),
    testNtfy: vi.fn(() => of({ message: '' })),
    testPushover: vi.fn(() => of({ message: '' })),
    testGotify: vi.fn(() => of({ message: 'Gotify test sent' })),
  };
}

function createToast() {
  return { success: vi.fn(), error: vi.fn() };
}

interface SetupOptions {
  initialType?: NotificationProviderType;
  editingProvider?: NotificationProviderDto;
  appriseCliStatus?: Observable<AppriseCliStatus>;
}

interface Setup {
  fixture: ComponentFixture<NotificationProviderModalComponent>;
  component: NotificationProviderModalComponent;
  api: ReturnType<typeof createApi>;
  toast: ReturnType<typeof createToast>;
  onSaved: ReturnType<typeof vi.fn>;
}

const ALL_LABEL_SELECTORS = '.input-label, .select-label, .chip-label, .number-label, .toggle__label';

describe('NotificationProviderModalComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(options: SetupOptions = {}): Setup {
    const api = createApi(options.appriseCliStatus);
    const toast = createToast();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        { provide: NotificationApi, useValue: api },
        { provide: ToastService, useValue: toast },
      ],
    });

    const fixture = TestBed.createComponent(NotificationProviderModalComponent);
    fixture.componentRef.setInput('editingProvider', options.editingProvider ?? null);
    fixture.componentRef.setInput('initialType', options.initialType ?? NotificationProviderType.Discord);
    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    const onSaved = vi.fn();
    fixture.componentInstance.saved.subscribe(onSaved);

    return { fixture, component: fixture.componentInstance, api, toast, onSaved };
  }

  function providerFieldLabels(fixture: ComponentFixture<NotificationProviderModalComponent>): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll(ALL_LABEL_SELECTORS))
      .filter((label) => !(label as HTMLElement).closest('.event-flags'))
      .map((label) => (label as HTMLElement).textContent!.trim())
      .filter((label) => label !== 'Enabled' && label !== 'Name');
  }

  function errorMessages(fixture: ComponentFixture<NotificationProviderModalComponent>): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.input-error, .chip-error, .number-error')).map(
      (error) => (error as HTMLElement).textContent!.trim(),
    );
  }

  function modalTitle(fixture: ComponentFixture<NotificationProviderModalComponent>): string {
    return (fixture.nativeElement.querySelector('.modal__title') as HTMLElement).textContent!.trim();
  }

  function footerButtons(fixture: ComponentFixture<NotificationProviderModalComponent>): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.modal__footer button'));
  }

  it('starts a new provider from the defaults and titles the modal for the initial type', () => {
    const { fixture, component } = setup({ initialType: NotificationProviderType.Ntfy });

    expect(modalTitle(fixture)).toBe('Add Ntfy Provider');
    expect(component.modalType()).toBe(NotificationProviderType.Ntfy);
    expect(component.modalModel()).toMatchObject({
      name: '',
      enabled: true,
      appriseMode: AppriseMode.Api,
      ntfyServerUrl: 'https://ntfy.sh',
      ntfyTopics: [],
      ntfyAuthType: NtfyAuthenticationType.None,
      ntfyPriority: NtfyPriority.Default,
      gotifyPriority: '5',
      pushoverPriority: PushoverPriority.Normal,
      pushoverRetry: 30,
      pushoverExpire: 3600,
      ...DEFAULT_EVENTS,
    });
    expect(component.hasPendingChanges()).toBe(false);
    expect(component.modalForm().invalid()).toBe(true);
  });

  it('hydrates the model from an existing provider without reporting pending changes', () => {
    const { fixture, component } = setup({ editingProvider: NTFY_PROVIDER });

    expect(modalTitle(fixture)).toBe('Edit Ntfy Provider');
    expect(component.modalType()).toBe(NotificationProviderType.Ntfy);
    expect(component.modalModel()).toMatchObject({
      name: 'My ntfy',
      enabled: false,
      ntfyServerUrl: 'https://ntfy.example.com',
      ntfyTopics: ['alpha', 'beta'],
      ntfyAuthType: NtfyAuthenticationType.BasicAuth,
      ntfyUsername: 'user',
      ntfyPassword: 'secret',
      ntfyAccessToken: '',
      ntfyPriority: NtfyPriority.High,
      ntfyTags: ['warning'],
      onFailedImportStrike: false,
      onStalledStrike: true,
      onCategoryChanged: true,
      onSearchItemGrabbed: true,
    });
    expect(component.hasPendingChanges()).toBe(false);
    expect(component.modalForm().invalid()).toBe(false);
  });

  const FIELD_SETS: { type: NotificationProviderType; labels: string[] }[] = [
    { type: NotificationProviderType.Discord, labels: ['Webhook URL', 'Username', 'Avatar URL'] },
    { type: NotificationProviderType.Telegram, labels: ['Bot Token', 'Chat ID', 'Topic ID', 'Send Silently'] },
    { type: NotificationProviderType.Notifiarr, labels: ['API Key', 'Channel ID'] },
    { type: NotificationProviderType.Apprise, labels: ['Mode', 'Server URL', 'Configuration Key', 'Tags'] },
    {
      type: NotificationProviderType.Ntfy,
      labels: ['Server URL', 'Topics', 'Authentication', 'Priority', 'Tags'],
    },
    {
      type: NotificationProviderType.Pushover,
      labels: ['API Token', 'User Key', 'Devices', 'Priority', 'Sound', 'Tags'],
    },
    {
      type: NotificationProviderType.Gotify,
      labels: ['Server URL', 'Application Token', 'Priority'],
    },
  ];

  it.each(FIELD_SETS)('renders only the $type field set', ({ type, labels }) => {
    const { fixture } = setup({ initialType: type });

    expect(providerFieldLabels(fixture)).toEqual(labels);
  });

  const REQUIRED_FIELDS: {
    type: NotificationProviderType;
    errors: string[];
    fill: (component: NotificationProviderModalComponent) => void;
  }[] = [
    {
      type: NotificationProviderType.Discord,
      errors: ['Webhook URL is required'],
      fill: (c) => c.modalModel.update((m) => ({ ...m, webhookUrl: 'https://discord.com/hook' })),
    },
    {
      type: NotificationProviderType.Telegram,
      errors: ['Bot token is required', 'Chat ID is required'],
      fill: (c) => c.modalModel.update((m) => ({ ...m, botToken: 'token', chatId: '-100' })),
    },
    {
      type: NotificationProviderType.Notifiarr,
      errors: ['API key is required'],
      fill: (c) => c.modalModel.update((m) => ({ ...m, apiKey: 'key' })),
    },
    {
      type: NotificationProviderType.Apprise,
      errors: ['Server URL is required', 'Config key is required'],
      fill: (c) => c.modalModel.update((m) => ({ ...m, appriseUrl: 'http://apprise', appriseKey: 'key' })),
    },
    {
      type: NotificationProviderType.Ntfy,
      errors: ['At least one topic is required'],
      fill: (c) => c.modalModel.update((m) => ({ ...m, ntfyTopics: ['topic'] })),
    },
    {
      type: NotificationProviderType.Pushover,
      errors: ['API token is required', 'User key is required'],
      fill: (c) => c.modalModel.update((m) => ({ ...m, pushoverApiToken: 'token', pushoverUserKey: 'key' })),
    },
    {
      type: NotificationProviderType.Gotify,
      errors: ['Server URL is required', 'Application token is required'],
      fill: (c) => c.modalModel.update((m) => ({ ...m, gotifyServerUrl: 'http://gotify', gotifyApplicationToken: 'token' })),
    },
  ];

  it.each(REQUIRED_FIELDS)('enforces the required $type fields before saving', ({ type, errors, fill }) => {
    const { fixture, component, api } = setup({ initialType: type });

    component.modalModel.update((m) => ({ ...m, name: 'Provider' }));
    fixture.detectChanges();

    expect(errorMessages(fixture)).toEqual(errors);
    expect(component.modalForm().invalid()).toBe(true);
    expect(footerButtons(fixture).every((button) => button.disabled)).toBe(true);

    component.saveProvider();
    expect(Object.values(api).some((method) => method.mock.calls.length > 0)).toBe(false);

    fill(component);
    fixture.detectChanges();

    expect(errorMessages(fixture)).toEqual([]);
    expect(component.modalForm().invalid()).toBe(false);
  });

  it('swaps the active validators when the provider type changes', () => {
    const { fixture, component } = setup({ initialType: NotificationProviderType.Discord });

    component.modalModel.update((m) => ({ ...m, name: 'Provider', webhookUrl: 'https://discord.com/hook' }));
    fixture.detectChanges();
    expect(component.modalForm().invalid()).toBe(false);

    component.modalType.set(NotificationProviderType.Gotify);
    fixture.detectChanges();

    expect(component.modalForm().invalid()).toBe(true);
    expect(component.modalForm.webhookUrl().errors()).toEqual([]);
    expect(errorMessages(fixture)).toEqual(['Server URL is required', 'Application token is required']);
  });

  it('switches the Apprise field set and validators between API and CLI mode', () => {
    const { fixture, component } = setup({ initialType: NotificationProviderType.Apprise });

    component.modalModel.update((m) => ({
      ...m,
      name: 'Provider',
      appriseUrl: 'http://apprise',
      appriseKey: 'key',
    }));
    fixture.detectChanges();
    expect(component.modalForm().invalid()).toBe(false);

    component.modalModel.update((m) => ({ ...m, appriseMode: AppriseMode.Cli }));
    fixture.detectChanges();

    expect(providerFieldLabels(fixture)).toEqual(['Mode', 'Service URLs', 'Tags']);
    expect(errorMessages(fixture)).toEqual(['At least one service URL is required']);

    component.modalModel.update((m) => ({ ...m, appriseServiceUrls: ['discord://id/token'] }));
    fixture.detectChanges();

    expect(component.modalForm().invalid()).toBe(false);
  });

  it('reports the detected Apprise CLI version once CLI mode is selected', () => {
    const { fixture, component, api } = setup({ initialType: NotificationProviderType.Apprise });

    expect(api.getAppriseCliStatus).not.toHaveBeenCalled();

    component.modalModel.update((m) => ({ ...m, appriseMode: AppriseMode.Cli }));
    fixture.detectChanges();

    expect(api.getAppriseCliStatus).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Apprise CLI detected: 1.9.0');
  });

  it('warns when the Apprise CLI is missing on the server', () => {
    const { fixture, component } = setup({
      initialType: NotificationProviderType.Apprise,
      appriseCliStatus: of({ available: false }),
    });

    component.modalModel.update((m) => ({ ...m, appriseMode: AppriseMode.Cli }));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Apprise CLI not found');
  });

  it('treats a failed Apprise CLI probe as unavailable and checks only once per session', () => {
    const { fixture, component, api } = setup({
      initialType: NotificationProviderType.Apprise,
      appriseCliStatus: throwError(() => new Error('offline')),
    });

    component.modalModel.update((m) => ({ ...m, appriseMode: AppriseMode.Cli }));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Apprise CLI not found');

    component.modalModel.update((m) => ({ ...m, appriseMode: AppriseMode.Api }));
    fixture.detectChanges();
    component.modalModel.update((m) => ({ ...m, appriseMode: AppriseMode.Cli }));
    fixture.detectChanges();

    expect(api.getAppriseCliStatus).toHaveBeenCalledTimes(1);
  });

  it('requires the ntfy credentials that match the selected authentication type', () => {
    const { fixture, component } = setup({ initialType: NotificationProviderType.Ntfy });

    component.modalModel.update((m) => ({ ...m, name: 'Provider', ntfyTopics: ['topic'] }));
    fixture.detectChanges();
    expect(component.modalForm().invalid()).toBe(false);

    component.modalModel.update((m) => ({ ...m, ntfyAuthType: NtfyAuthenticationType.BasicAuth }));
    fixture.detectChanges();

    expect(providerFieldLabels(fixture)).toContain('Username');
    expect(providerFieldLabels(fixture)).toContain('Password');
    expect(errorMessages(fixture)).toEqual(['Username is required', 'Password is required']);

    component.modalModel.update((m) => ({ ...m, ntfyAuthType: NtfyAuthenticationType.AccessToken }));
    fixture.detectChanges();

    expect(providerFieldLabels(fixture)).toContain('Access Token');
    expect(providerFieldLabels(fixture)).not.toContain('Username');
    expect(errorMessages(fixture)).toEqual(['Access token is required']);

    component.modalModel.update((m) => ({ ...m, ntfyAccessToken: 'tk_123' }));
    fixture.detectChanges();

    expect(component.modalForm().invalid()).toBe(false);
  });

  it('bounds the Pushover retry and expire fields only for emergency priority', () => {
    const { fixture, component } = setup({ initialType: NotificationProviderType.Pushover });

    component.modalModel.update((m) => ({
      ...m,
      name: 'Provider',
      pushoverApiToken: 'token',
      pushoverUserKey: 'key',
      pushoverRetry: 5,
      pushoverExpire: 99999,
    }));
    fixture.detectChanges();

    expect(component.modalForm().invalid()).toBe(false);
    expect(providerFieldLabels(fixture)).not.toContain('Retry (seconds)');

    component.modalModel.update((m) => ({ ...m, pushoverPriority: PushoverPriority.Emergency }));
    fixture.detectChanges();

    expect(providerFieldLabels(fixture)).toContain('Retry (seconds)');
    expect(errorMessages(fixture)).toEqual(['Minimum 30 seconds', 'Maximum 10800 seconds']);

    component.modalModel.update((m) => ({ ...m, pushoverRetry: 60, pushoverExpire: 0 }));
    fixture.detectChanges();

    expect(errorMessages(fixture)).toEqual(['Minimum 1 second']);

    component.modalModel.update((m) => ({ ...m, pushoverExpire: 3600 }));
    fixture.detectChanges();

    expect(component.modalForm().invalid()).toBe(false);
  });

  it('parses the Gotify priority into a number and falls back to 5 when it is not numeric', () => {
    const { fixture, component, api } = setup({ editingProvider: GOTIFY_PROVIDER });

    expect(component.modalModel().gotifyPriority).toBe('8');

    component.saveProvider();
    fixture.detectChanges();

    expect(api.updateGotify).toHaveBeenCalledWith('gotify-1', {
      name: 'My gotify',
      serverUrl: 'https://gotify.example.com',
      applicationToken: 'token',
      priority: 8,
      isEnabled: true,
      ...DEFAULT_EVENTS,
    });

    component.modalModel.update((m) => ({ ...m, gotifyPriority: 'not-a-number' }));
    fixture.detectChanges();
    component.saveProvider();

    expect(api.updateGotify).toHaveBeenLastCalledWith('gotify-1', expect.objectContaining({ priority: 5 }));
  });

  it('sends a test notification with the current values and toasts the server message', () => {
    const { fixture, component, api, toast } = setup({ editingProvider: DISCORD_PROVIDER });

    (fixture.nativeElement.querySelector('.modal__footer button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(api.testDiscord).toHaveBeenCalledWith({
      webhookUrl: 'https://discord.com/api/webhooks/abc',
      username: 'Cleanuparr',
      avatarUrl: undefined,
      providerId: 'discord-1',
    });
    expect(toast.success).toHaveBeenCalledWith('Discord test sent');
    expect(component.testing()).toBe(false);
  });

  it('falls back to a generic message and reports a failed test notification', () => {
    const { fixture, component, api, toast } = setup({ initialType: NotificationProviderType.Ntfy });

    component.modalModel.update((m) => ({ ...m, name: 'Provider', ntfyTopics: ['topic'] }));
    fixture.detectChanges();

    component.testNotification();
    fixture.detectChanges();

    expect(api.testNtfy).toHaveBeenCalledWith({
      serverUrl: 'https://ntfy.sh',
      topics: ['topic'],
      authenticationType: NtfyAuthenticationType.None,
      username: undefined,
      password: undefined,
      accessToken: undefined,
      priority: NtfyPriority.Default,
      tags: undefined,
      providerId: undefined,
    });
    expect(toast.success).toHaveBeenCalledWith('Test sent');

    api.testNtfy.mockReturnValue(throwError(() => new Error('offline')));
    component.testNotification();
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Test failed');
    expect(component.testing()).toBe(false);
  });

  it('creates a Discord provider, closes the modal and emits saved', () => {
    const { fixture, component, api, toast, onSaved } = setup({ initialType: NotificationProviderType.Discord });

    component.modalModel.update((m) => ({
      ...m,
      name: 'Alerts',
      webhookUrl: 'https://discord.com/hook',
      avatarUrl: 'https://example.com/a.png',
      onCategoryChanged: true,
    }));
    fixture.detectChanges();

    (footerButtons(fixture)[1]).click();
    fixture.detectChanges();

    expect(api.createDiscord).toHaveBeenCalledWith({
      name: 'Alerts',
      webhookUrl: 'https://discord.com/hook',
      username: undefined,
      avatarUrl: 'https://example.com/a.png',
      isEnabled: true,
      ...DEFAULT_EVENTS,
      onCategoryChanged: true,
    });
    expect(api.updateDiscord).not.toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalledWith('Provider added');
    expect(component.visible()).toBe(false);
    expect(component.saving()).toBe(false);
    expect(onSaved).toHaveBeenCalledTimes(1);
  });

  it('updates an existing ntfy provider with the loaded configuration', () => {
    const { fixture, component, api, toast, onSaved } = setup({ editingProvider: NTFY_PROVIDER });

    component.modalModel.update((m) => ({ ...m, ntfyTopics: ['alpha'] }));
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(true);

    component.saveProvider();
    fixture.detectChanges();

    expect(api.updateNtfy).toHaveBeenCalledWith('ntfy-1', {
      name: 'My ntfy',
      serverUrl: 'https://ntfy.example.com',
      topics: ['alpha'],
      authenticationType: NtfyAuthenticationType.BasicAuth,
      username: 'user',
      password: 'secret',
      accessToken: undefined,
      priority: NtfyPriority.High,
      tags: ['warning'],
      isEnabled: false,
      ...NTFY_PROVIDER.events,
    });
    expect(toast.success).toHaveBeenCalledWith('Provider updated');
    expect(onSaved).toHaveBeenCalledTimes(1);
  });

  it('maps the Pushover custom sound and drops retry and expire outside emergency priority', () => {
    const { fixture, component, api } = setup({ initialType: NotificationProviderType.Pushover });

    component.modalModel.update((m) => ({
      ...m,
      name: 'Phone',
      pushoverApiToken: 'token',
      pushoverUserKey: 'key',
      pushoverDevices: ['myphone'],
      pushoverSound: '__custom__',
      pushoverCustomSound: 'siren-long',
      pushoverTags: ['tag1'],
    }));
    fixture.detectChanges();

    component.saveProvider();

    expect(api.createPushover).toHaveBeenCalledWith({
      name: 'Phone',
      apiToken: 'token',
      userKey: 'key',
      devices: ['myphone'],
      priority: PushoverPriority.Normal,
      sound: 'siren-long',
      retry: undefined,
      expire: undefined,
      tags: ['tag1'],
      isEnabled: true,
      ...DEFAULT_EVENTS,
    });

    component.modalModel.update((m) => ({
      ...m,
      pushoverPriority: PushoverPriority.Emergency,
      pushoverSound: 'siren',
    }));
    fixture.detectChanges();
    component.saveProvider();

    expect(api.createPushover).toHaveBeenLastCalledWith(
      expect.objectContaining({ sound: 'siren', retry: 30, expire: 3600 }),
    );
  });

  it('keeps the modal open and toasts when the save request fails', () => {
    const { fixture, component, api, toast, onSaved } = setup({ initialType: NotificationProviderType.Notifiarr });

    component.modalModel.update((m) => ({ ...m, name: 'Provider', apiKey: 'key' }));
    fixture.detectChanges();

    api.createNotifiarr.mockReturnValue(throwError(() => new Error('boom')));
    component.saveProvider();
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Failed to save provider');
    expect(toast.success).not.toHaveBeenCalled();
    expect(component.visible()).toBe(true);
    expect(component.saving()).toBe(false);
    expect(onSaved).not.toHaveBeenCalled();
  });

  it('tracks pending changes only while the modal is open', () => {
    const { fixture, component } = setup({ editingProvider: DISCORD_PROVIDER });

    expect(component.hasPendingChanges()).toBe(false);

    component.modalModel.update((m) => ({ ...m, username: 'Renamed' }));
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(true);

    component.visible.set(false);
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(false);
  });
});
