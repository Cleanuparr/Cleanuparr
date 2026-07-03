import { Component, ChangeDetectionStrategy, inject, signal, input, model, output, effect, untracked } from '@angular/core';
import { form, validate, FormField } from '@angular/forms/signals';
import {
  ButtonComponent, InputComponent, ToggleComponent, SelectComponent,
  ModalComponent, ChipInputComponent, NumberInputComponent,
  type SelectOption,
} from '@ui';
import { NotificationApi } from '@core/api/notification.api';
import { ToastService } from '@core/services/toast.service';
import {
  NotificationProviderDto,
  CreateDiscordProviderRequest,
  CreateTelegramProviderRequest,
  CreateNotifiarrProviderRequest,
  CreateAppriseProviderRequest,
  CreateNtfyProviderRequest,
  CreatePushoverProviderRequest,
  CreateGotifyProviderRequest,
} from '@shared/models/notification-provider.model';
import {
  NotificationProviderType,
  AppriseMode,
  NtfyAuthenticationType,
  NtfyPriority,
  PushoverPriority,
} from '@shared/models/enums';

interface ProviderConfiguration {
  webhookUrl?: string;
  username?: string;
  avatarUrl?: string;
  botToken?: string;
  chatId?: string;
  topicId?: string;
  sendSilently?: boolean;
  apiKey?: string;
  channelId?: string;
  mode?: AppriseMode;
  url?: string;
  key?: string;
  tags?: string | string[];
  serviceUrls?: string;
  serverUrl?: string;
  topics?: string[];
  authenticationType?: NtfyAuthenticationType;
  password?: string;
  accessToken?: string;
  priority?: number | NtfyPriority | PushoverPriority;
  apiToken?: string;
  userKey?: string;
  devices?: string[];
  sound?: string;
  customSound?: string;
  retry?: number;
  expire?: number;
  applicationToken?: string;
}

interface NotificationModalModel {
  name: string;
  enabled: boolean;
  // Discord
  webhookUrl: string;
  username: string;
  avatarUrl: string;
  // Telegram
  botToken: string;
  chatId: string;
  topicId: string;
  sendSilently: boolean;
  // Notifiarr
  apiKey: string;
  channelId: string;
  // Apprise
  appriseMode: AppriseMode;
  appriseUrl: string;
  appriseKey: string;
  appriseTags: string;
  appriseServiceUrls: string[];
  // Ntfy
  ntfyServerUrl: string;
  ntfyTopics: string[];
  ntfyAuthType: NtfyAuthenticationType;
  ntfyUsername: string;
  ntfyPassword: string;
  ntfyAccessToken: string;
  ntfyPriority: NtfyPriority;
  ntfyTags: string[];
  // Gotify
  gotifyServerUrl: string;
  gotifyApplicationToken: string;
  gotifyPriority: string;
  // Pushover
  pushoverApiToken: string;
  pushoverUserKey: string;
  pushoverDevices: string[];
  pushoverPriority: PushoverPriority;
  pushoverRetry: number | null;
  pushoverExpire: number | null;
  pushoverSound: string;
  pushoverCustomSound: string;
  pushoverTags: string[];
  // Events
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  onSearchTriggered: boolean;
  onSearchItemGrabbed: boolean;
}

function createDefaultModalModel(): NotificationModalModel {
  return {
    name: '',
    enabled: true,
    webhookUrl: '', username: '', avatarUrl: '',
    botToken: '', chatId: '', topicId: '', sendSilently: false,
    apiKey: '', channelId: '',
    appriseMode: AppriseMode.Api, appriseUrl: '', appriseKey: '', appriseTags: '', appriseServiceUrls: [],
    ntfyServerUrl: 'https://ntfy.sh', ntfyTopics: [], ntfyAuthType: NtfyAuthenticationType.None,
    ntfyUsername: '', ntfyPassword: '', ntfyAccessToken: '', ntfyPriority: NtfyPriority.Default, ntfyTags: [],
    gotifyServerUrl: '', gotifyApplicationToken: '', gotifyPriority: '5',
    pushoverApiToken: '', pushoverUserKey: '', pushoverDevices: [], pushoverPriority: PushoverPriority.Normal,
    pushoverRetry: 30, pushoverExpire: 3600, pushoverSound: '', pushoverCustomSound: '', pushoverTags: [],
    onFailedImportStrike: true, onStalledStrike: true, onSlowStrike: true, onQueueItemDeleted: true,
    onDownloadCleaned: true, onCategoryChanged: false, onSearchTriggered: false, onSearchItemGrabbed: false,
  };
}

function parseGotifyPriority(value: string): number {
  const priority = Number.parseInt(value, 10);
  return Number.isNaN(priority) ? 5 : priority;
}

const APPRISE_MODE_OPTIONS: SelectOption[] = [
  { label: 'API', value: AppriseMode.Api },
  { label: 'CLI', value: AppriseMode.Cli },
];

const NTFY_AUTH_OPTIONS: SelectOption[] = [
  { label: 'None', value: NtfyAuthenticationType.None },
  { label: 'Basic Auth', value: NtfyAuthenticationType.BasicAuth },
  { label: 'Access Token', value: NtfyAuthenticationType.AccessToken },
];

const NTFY_PRIORITY_OPTIONS: SelectOption[] = [
  { label: 'Min', value: NtfyPriority.Min },
  { label: 'Low', value: NtfyPriority.Low },
  { label: 'Default', value: NtfyPriority.Default },
  { label: 'High', value: NtfyPriority.High },
  { label: 'Max', value: NtfyPriority.Max },
];

const GOTIFY_PRIORITY_OPTIONS: SelectOption[] = [
  { label: '0', value: '0' },
  { label: '1', value: '1' },
  { label: '2', value: '2' },
  { label: '3', value: '3' },
  { label: '4', value: '4' },
  { label: '5 (Default)', value: '5' },
  { label: '6', value: '6' },
  { label: '7', value: '7' },
  { label: '8', value: '8' },
  { label: '9', value: '9' },
  { label: '10', value: '10' },
];

const PUSHOVER_PRIORITY_OPTIONS: SelectOption[] = [
  { label: 'Lowest', value: PushoverPriority.Lowest },
  { label: 'Low', value: PushoverPriority.Low },
  { label: 'Normal', value: PushoverPriority.Normal },
  { label: 'High', value: PushoverPriority.High },
  { label: 'Emergency', value: PushoverPriority.Emergency },
];

const PUSHOVER_SOUND_OPTIONS: SelectOption[] = [
  { label: '(Use default)', value: '' },
  { label: 'Pushover (Default)', value: 'pushover' },
  { label: 'Bike', value: 'bike' },
  { label: 'Bugle', value: 'bugle' },
  { label: 'Cash Register', value: 'cashregister' },
  { label: 'Classical', value: 'classical' },
  { label: 'Cosmic', value: 'cosmic' },
  { label: 'Falling', value: 'falling' },
  { label: 'Gamelan', value: 'gamelan' },
  { label: 'Incoming', value: 'incoming' },
  { label: 'Intermission', value: 'intermission' },
  { label: 'Magic', value: 'magic' },
  { label: 'Mechanical', value: 'mechanical' },
  { label: 'Piano Bar', value: 'pianobar' },
  { label: 'Siren', value: 'siren' },
  { label: 'Space Alarm', value: 'spacealarm' },
  { label: 'Tugboat', value: 'tugboat' },
  { label: 'Alien (Long)', value: 'alien' },
  { label: 'Climb (Long)', value: 'climb' },
  { label: 'Persistent (Long)', value: 'persistent' },
  { label: 'Echo (Long)', value: 'echo' },
  { label: 'Up Down (Long)', value: 'updown' },
  { label: 'Vibrate Only', value: 'vibrate' },
  { label: 'Silent', value: 'none' },
  { label: 'Custom...', value: '__custom__' },
];

@Component({
  selector: 'app-notification-provider-modal',
  standalone: true,
  imports: [
    ButtonComponent, InputComponent, ToggleComponent, SelectComponent,
    ModalComponent, ChipInputComponent, NumberInputComponent, FormField,
  ],
  templateUrl: './notification-provider-modal.component.html',
  styleUrl: './notification-provider-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationProviderModalComponent {
  private readonly api = inject(NotificationApi);
  private readonly toast = inject(ToastService);

  readonly editingProvider = input<NotificationProviderDto | null>(null);
  readonly initialType = input<NotificationProviderType>(NotificationProviderType.Discord);
  readonly visible = model(false);
  readonly saved = output<void>();

  readonly modalType = signal<NotificationProviderType>(NotificationProviderType.Discord);
  readonly testing = signal(false);
  readonly saving = signal(false);

  readonly modalModel = signal<NotificationModalModel>(createDefaultModalModel());
  readonly modalForm = form(this.modalModel, (p) => {
    validate(p.name, () =>
      !this.modalModel().name.trim() ? { kind: 'required', message: 'Name is required' } : undefined);

    // Discord
    validate(p.webhookUrl, () =>
      this.modalType() === NotificationProviderType.Discord && !this.modalModel().webhookUrl.trim()
        ? { kind: 'required', message: 'Webhook URL is required' } : undefined);

    // Telegram
    validate(p.botToken, () =>
      this.modalType() === NotificationProviderType.Telegram && !this.modalModel().botToken.trim()
        ? { kind: 'required', message: 'Bot token is required' } : undefined);
    validate(p.chatId, () =>
      this.modalType() === NotificationProviderType.Telegram && !this.modalModel().chatId.trim()
        ? { kind: 'required', message: 'Chat ID is required' } : undefined);

    // Notifiarr
    validate(p.apiKey, () =>
      this.modalType() === NotificationProviderType.Notifiarr && !this.modalModel().apiKey.trim()
        ? { kind: 'required', message: 'API key is required' } : undefined);

    // Apprise
    validate(p.appriseUrl, () =>
      this.modalType() === NotificationProviderType.Apprise && this.modalModel().appriseMode === AppriseMode.Api && !this.modalModel().appriseUrl.trim()
        ? { kind: 'required', message: 'Server URL is required' } : undefined);
    validate(p.appriseKey, () =>
      this.modalType() === NotificationProviderType.Apprise && this.modalModel().appriseMode === AppriseMode.Api && !this.modalModel().appriseKey.trim()
        ? { kind: 'required', message: 'Config key is required' } : undefined);
    validate(p.appriseServiceUrls, () =>
      this.modalType() === NotificationProviderType.Apprise && this.modalModel().appriseMode === AppriseMode.Cli && this.modalModel().appriseServiceUrls.length === 0
        ? { kind: 'required', message: 'At least one service URL is required' } : undefined);

    // Ntfy
    validate(p.ntfyServerUrl, () =>
      this.modalType() === NotificationProviderType.Ntfy && !this.modalModel().ntfyServerUrl.trim()
        ? { kind: 'required', message: 'Server URL is required' } : undefined);
    validate(p.ntfyTopics, () =>
      this.modalType() === NotificationProviderType.Ntfy && this.modalModel().ntfyTopics.length === 0
        ? { kind: 'required', message: 'At least one topic is required' } : undefined);

    // Pushover
    validate(p.pushoverApiToken, () =>
      this.modalType() === NotificationProviderType.Pushover && !this.modalModel().pushoverApiToken.trim()
        ? { kind: 'required', message: 'API token is required' } : undefined);
    validate(p.pushoverUserKey, () =>
      this.modalType() === NotificationProviderType.Pushover && !this.modalModel().pushoverUserKey.trim()
        ? { kind: 'required', message: 'User key is required' } : undefined);
    // Retry/expire only apply to Emergency priority; skip otherwise so stale
    // values from a hidden field can't keep the modal Save disabled.
    validate(p.pushoverRetry, () => {
      if (this.modalType() !== NotificationProviderType.Pushover
        || this.modalModel().pushoverPriority !== PushoverPriority.Emergency) {
        return undefined;
      }
      const retry = this.modalModel().pushoverRetry;
      return retry == null || retry < 30 ? { kind: 'min', message: 'Minimum 30 seconds' } : undefined;
    });
    validate(p.pushoverExpire, () => {
      if (this.modalType() !== NotificationProviderType.Pushover
        || this.modalModel().pushoverPriority !== PushoverPriority.Emergency) {
        return undefined;
      }
      const expire = this.modalModel().pushoverExpire;
      if (expire == null || expire < 1) return { kind: 'min', message: 'Minimum 1 second' };
      if (expire > 10800) return { kind: 'max', message: 'Maximum 10800 seconds' };
      return undefined;
    });

    // Gotify
    validate(p.gotifyServerUrl, () =>
      this.modalType() === NotificationProviderType.Gotify && !this.modalModel().gotifyServerUrl.trim()
        ? { kind: 'required', message: 'Server URL is required' } : undefined);
    validate(p.gotifyApplicationToken, () =>
      this.modalType() === NotificationProviderType.Gotify && !this.modalModel().gotifyApplicationToken.trim()
        ? { kind: 'required', message: 'Application token is required' } : undefined);
  });

  // Options (exposed for template)
  readonly gotifyPriorityOptions = GOTIFY_PRIORITY_OPTIONS;
  readonly appriseOptions = APPRISE_MODE_OPTIONS;
  readonly ntfyAuthOptions = NTFY_AUTH_OPTIONS;
  readonly ntfyPriorityOptions = NTFY_PRIORITY_OPTIONS;
  readonly pushoverPriorityOptions = PUSHOVER_PRIORITY_OPTIONS;
  readonly pushoverSoundOptions = PUSHOVER_SOUND_OPTIONS;

  constructor() {
    // Populate the form from the input provider (or defaults) each time the modal opens.
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const provider = untracked(() => this.editingProvider());
      const initialType = untracked(() => this.initialType());
      untracked(() => {
        if (provider) {
          this.modalType.set(provider.type);
          this.modalModel.set(this.buildModelFromProvider(provider));
        } else {
          this.modalType.set(initialType);
          this.modalModel.set(createDefaultModalModel());
        }
      });
    });
  }

  private buildModelFromProvider(provider: NotificationProviderDto): NotificationModalModel {
    const config = provider.configuration as ProviderConfiguration;
    const model = createDefaultModalModel();
    model.name = provider.name;
    model.enabled = provider.isEnabled;

    switch (provider.type) {
      case NotificationProviderType.Discord:
        model.webhookUrl = config.webhookUrl ?? '';
        model.username = config.username ?? '';
        model.avatarUrl = config.avatarUrl ?? '';
        break;
      case NotificationProviderType.Telegram:
        model.botToken = config.botToken ?? '';
        model.chatId = config.chatId ?? '';
        model.topicId = config.topicId ?? '';
        model.sendSilently = config.sendSilently ?? false;
        break;
      case NotificationProviderType.Notifiarr:
        model.apiKey = config.apiKey ?? '';
        model.channelId = config.channelId ?? '';
        break;
      case NotificationProviderType.Apprise:
        model.appriseMode = config.mode ?? AppriseMode.Api;
        model.appriseUrl = config.url ?? '';
        model.appriseKey = config.key ?? '';
        model.appriseTags = (config.tags as string) ?? '';
        model.appriseServiceUrls = config.serviceUrls ? config.serviceUrls.split('\n').filter((s: string) => s.trim()) : [];
        break;
      case NotificationProviderType.Ntfy:
        model.ntfyServerUrl = config.serverUrl ?? 'https://ntfy.sh';
        model.ntfyTopics = config.topics ?? [];
        model.ntfyAuthType = config.authenticationType ?? NtfyAuthenticationType.None;
        model.ntfyUsername = config.username ?? '';
        model.ntfyPassword = config.password ?? '';
        model.ntfyAccessToken = config.accessToken ?? '';
        model.ntfyPriority = Object.values(NtfyPriority).includes(config.priority as NtfyPriority)
          ? (config.priority as NtfyPriority)
          : NtfyPriority.Default;
        model.ntfyTags = (config.tags as string[]) ?? [];
        break;
      case NotificationProviderType.Pushover:
        model.pushoverApiToken = config.apiToken ?? '';
        model.pushoverUserKey = config.userKey ?? '';
        model.pushoverDevices = config.devices ?? [];
        model.pushoverPriority = Object.values(PushoverPriority).includes(config.priority as PushoverPriority)
          ? (config.priority as PushoverPriority)
          : PushoverPriority.Normal;
        model.pushoverRetry = config.retry ?? 30;
        model.pushoverExpire = config.expire ?? 3600;
        model.pushoverSound = config.sound ?? '';
        model.pushoverCustomSound = config.customSound ?? '';
        model.pushoverTags = (config.tags as string[]) ?? [];
        break;
      case NotificationProviderType.Gotify:
        model.gotifyServerUrl = config.serverUrl ?? '';
        model.gotifyApplicationToken = config.applicationToken ?? '';
        model.gotifyPriority = String(config.priority ?? 5);
        break;
    }

    model.onFailedImportStrike = provider.events.onFailedImportStrike;
    model.onStalledStrike = provider.events.onStalledStrike;
    model.onSlowStrike = provider.events.onSlowStrike;
    model.onQueueItemDeleted = provider.events.onQueueItemDeleted;
    model.onDownloadCleaned = provider.events.onDownloadCleaned;
    model.onCategoryChanged = provider.events.onCategoryChanged;
    model.onSearchTriggered = provider.events.onSearchTriggered;
    model.onSearchItemGrabbed = provider.events.onSearchItemGrabbed;

    return model;
  }

  private getEventFlags() {
    const m = this.modalModel();
    return {
      onFailedImportStrike: m.onFailedImportStrike,
      onStalledStrike: m.onStalledStrike,
      onSlowStrike: m.onSlowStrike,
      onQueueItemDeleted: m.onQueueItemDeleted,
      onDownloadCleaned: m.onDownloadCleaned,
      onCategoryChanged: m.onCategoryChanged,
      onSearchTriggered: m.onSearchTriggered,
      onSearchItemGrabbed: m.onSearchItemGrabbed,
    };
  }

  testNotification(): void {
    const type = this.modalType();
    const m = this.modalModel();
    this.testing.set(true);
    const providerId = this.editingProvider()?.id;

    switch (type) {
      case NotificationProviderType.Discord:
        this.api.testDiscord({
          webhookUrl: m.webhookUrl,
          username: m.username || undefined,
          avatarUrl: m.avatarUrl || undefined,
          providerId,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Telegram:
        this.api.testTelegram({
          botToken: m.botToken,
          chatId: m.chatId,
          topicId: m.topicId || undefined,
          sendSilently: m.sendSilently,
          providerId,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Notifiarr:
        this.api.testNotifiarr({
          apiKey: m.apiKey,
          channelId: m.channelId,
          providerId,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Apprise:
        this.api.testApprise({
          mode: m.appriseMode,
          url: m.appriseUrl || undefined,
          key: m.appriseKey || undefined,
          tags: m.appriseTags || undefined,
          serviceUrls: m.appriseServiceUrls.join('\n') || undefined,
          providerId,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Ntfy:
        this.api.testNtfy({
          serverUrl: m.ntfyServerUrl,
          topics: m.ntfyTopics,
          authenticationType: m.ntfyAuthType,
          username: m.ntfyUsername || undefined,
          password: m.ntfyPassword || undefined,
          accessToken: m.ntfyAccessToken || undefined,
          priority: m.ntfyPriority,
          tags: m.ntfyTags.length > 0 ? m.ntfyTags : undefined,
          providerId,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Pushover: {
        const sound = m.pushoverSound;
        this.api.testPushover({
          apiToken: m.pushoverApiToken,
          userKey: m.pushoverUserKey,
          devices: m.pushoverDevices.length > 0 ? m.pushoverDevices : undefined,
          priority: m.pushoverPriority,
          sound: sound === '__custom__' ? m.pushoverCustomSound : (sound || undefined),
          retry: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverRetry ?? 30) : undefined,
          expire: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverExpire ?? 3600) : undefined,
          tags: m.pushoverTags.length > 0 ? m.pushoverTags : undefined,
          providerId,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      }
      case NotificationProviderType.Gotify:
        this.api.testGotify({
          serverUrl: m.gotifyServerUrl,
          applicationToken: m.gotifyApplicationToken,
          priority: parseGotifyPriority(m.gotifyPriority),
          providerId,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
    }
  }

  saveProvider(): void {
    if (this.modalForm().invalid()) return;
    const type = this.modalType();
    const m = this.modalModel();
    const editing = this.editingProvider();
    this.saving.set(true);
    const eventFlags = this.getEventFlags();

    switch (type) {
      case NotificationProviderType.Discord: {
        const request: CreateDiscordProviderRequest = {
          name: m.name,
          webhookUrl: m.webhookUrl,
          username: m.username || undefined,
          avatarUrl: m.avatarUrl || undefined,
          isEnabled: m.enabled,
          ...eventFlags,
        };
        const obs = editing ? this.api.updateDiscord(editing.id, request) : this.api.createDiscord(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Telegram: {
        const request: CreateTelegramProviderRequest = {
          name: m.name,
          botToken: m.botToken,
          chatId: m.chatId,
          topicId: m.topicId || undefined,
          sendSilently: m.sendSilently,
          isEnabled: m.enabled,
          ...eventFlags,
        };
        const obs = editing ? this.api.updateTelegram(editing.id, request) : this.api.createTelegram(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Notifiarr: {
        const request: CreateNotifiarrProviderRequest = {
          name: m.name,
          apiKey: m.apiKey,
          channelId: m.channelId,
          isEnabled: m.enabled,
          ...eventFlags,
        };
        const obs = editing ? this.api.updateNotifiarr(editing.id, request) : this.api.createNotifiarr(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Apprise: {
        const request: CreateAppriseProviderRequest = {
          name: m.name,
          mode: m.appriseMode,
          url: m.appriseUrl || undefined,
          key: m.appriseKey || undefined,
          tags: m.appriseTags || undefined,
          serviceUrls: m.appriseServiceUrls.join('\n') || undefined,
          isEnabled: m.enabled,
          ...eventFlags,
        };
        const obs = editing ? this.api.updateApprise(editing.id, request) : this.api.createApprise(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Ntfy: {
        const request: CreateNtfyProviderRequest = {
          name: m.name,
          serverUrl: m.ntfyServerUrl,
          topics: m.ntfyTopics,
          authenticationType: m.ntfyAuthType,
          username: m.ntfyUsername || undefined,
          password: m.ntfyPassword || undefined,
          accessToken: m.ntfyAccessToken || undefined,
          priority: m.ntfyPriority,
          tags: m.ntfyTags.length > 0 ? m.ntfyTags : undefined,
          isEnabled: m.enabled,
          ...eventFlags,
        };
        const obs = editing ? this.api.updateNtfy(editing.id, request) : this.api.createNtfy(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Pushover: {
        const sound = m.pushoverSound;
        const request: CreatePushoverProviderRequest = {
          name: m.name,
          apiToken: m.pushoverApiToken,
          userKey: m.pushoverUserKey,
          devices: m.pushoverDevices.length > 0 ? m.pushoverDevices : undefined,
          priority: m.pushoverPriority,
          sound: sound === '__custom__' ? m.pushoverCustomSound : (sound || undefined),
          retry: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverRetry ?? 30) : undefined,
          expire: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverExpire ?? 3600) : undefined,
          tags: m.pushoverTags.length > 0 ? m.pushoverTags : undefined,
          isEnabled: m.enabled,
          ...eventFlags,
        };
        const obs = editing ? this.api.updatePushover(editing.id, request) : this.api.createPushover(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Gotify: {
        const request: CreateGotifyProviderRequest = {
          name: m.name,
          serverUrl: m.gotifyServerUrl,
          applicationToken: m.gotifyApplicationToken,
          priority: parseGotifyPriority(m.gotifyPriority),
          isEnabled: m.enabled,
          ...eventFlags,
        };
        const obs = editing ? this.api.updateGotify(editing.id, request) : this.api.createGotify(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
    }
  }

  private onSaveSuccess(editing: NotificationProviderDto | null): void {
    this.toast.success(editing ? 'Provider updated' : 'Provider added');
    this.visible.set(false);
    this.saving.set(false);
    this.saved.emit();
  }

  private onSaveError(): void {
    this.toast.error('Failed to save provider');
    this.saving.set(false);
  }
}
