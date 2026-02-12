import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent,
  ChipInputComponent, NumberInputComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { NotificationApi } from '@core/api/notification.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import {
  NotificationProviderDto,
  CreateDiscordProviderRequest,
  CreateTelegramProviderRequest,
  CreateNotifiarrProviderRequest,
  CreateAppriseProviderRequest,
  CreateNtfyProviderRequest,
  CreatePushoverProviderRequest,
} from '@shared/models/notification-provider.model';
import {
  NotificationProviderType,
  AppriseMode,
  NtfyAuthenticationType,
  NtfyPriority,
  PushoverPriority,
} from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';

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
  selector: 'app-notifications',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, SelectComponent, ModalComponent, EmptyStateComponent,
    BadgeComponent, ChipInputComponent, NumberInputComponent, LoadingStateComponent,
  ],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(NotificationApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly providers = signal<NotificationProviderDto[]>([]);

  // Selection modal
  readonly selectionModalVisible = signal(false);

  // Config modal
  readonly modalVisible = signal(false);
  readonly editingProvider = signal<NotificationProviderDto | null>(null);
  readonly modalType = signal<NotificationProviderType>(NotificationProviderType.Discord);
  readonly modalName = signal('');
  readonly modalEnabled = signal(true);
  readonly testing = signal(false);

  // Discord fields
  readonly modalWebhookUrl = signal('');
  readonly modalUsername = signal('');
  readonly modalAvatarUrl = signal('');

  // Telegram fields
  readonly modalBotToken = signal('');
  readonly modalChatId = signal('');
  readonly modalTopicId = signal('');
  readonly modalSendSilently = signal(false);

  // Notifiarr fields
  readonly modalApiKey = signal('');
  readonly modalChannelId = signal('');

  // Apprise fields
  readonly modalAppriseMode = signal<unknown>(AppriseMode.Api);
  readonly modalAppriseUrl = signal('');
  readonly modalAppriseKey = signal('');
  readonly modalAppriseTags = signal('');
  readonly modalAppriseServiceUrls = signal<string[]>([]);

  // Ntfy fields
  readonly modalNtfyServerUrl = signal('https://ntfy.sh');
  readonly modalNtfyTopics = signal<string[]>([]);
  readonly modalNtfyAuthType = signal<unknown>(NtfyAuthenticationType.None);
  readonly modalNtfyUsername = signal('');
  readonly modalNtfyPassword = signal('');
  readonly modalNtfyAccessToken = signal('');
  readonly modalNtfyPriority = signal<unknown>(NtfyPriority.Default);
  readonly modalNtfyTags = signal<string[]>([]);

  // Pushover fields
  readonly modalPushoverApiToken = signal('');
  readonly modalPushoverUserKey = signal('');
  readonly modalPushoverDevices = signal<string[]>([]);
  readonly modalPushoverPriority = signal<unknown>(PushoverPriority.Normal);
  readonly modalPushoverRetry = signal<number | null>(30);
  readonly modalPushoverExpire = signal<number | null>(3600);
  readonly modalPushoverSound = signal<unknown>('');
  readonly modalPushoverCustomSound = signal('');
  readonly modalPushoverTags = signal<string[]>([]);

  // Event flags
  readonly onFailedImportStrike = signal(true);
  readonly onStalledStrike = signal(true);
  readonly onSlowStrike = signal(true);
  readonly onQueueItemDeleted = signal(true);
  readonly onDownloadCleaned = signal(true);
  readonly onCategoryChanged = signal(false);

  // Modal validation
  readonly modalNameError = computed(() => {
    if (!this.modalName().trim()) return 'Name is required';
    return undefined;
  });

  readonly hasModalErrors = computed(() => {
    if (this.modalNameError()) return true;
    const type = this.modalType();
    switch (type) {
      case NotificationProviderType.Discord:
        return !this.modalWebhookUrl().trim();
      case NotificationProviderType.Telegram:
        return !this.modalBotToken().trim() || !this.modalChatId().trim();
      case NotificationProviderType.Notifiarr:
        return !this.modalApiKey().trim();
      case NotificationProviderType.Apprise:
        if ((this.modalAppriseMode() as AppriseMode) === AppriseMode.Api) {
          return !this.modalAppriseUrl().trim() || !this.modalAppriseKey().trim();
        }
        return this.modalAppriseServiceUrls().length === 0;
      case NotificationProviderType.Ntfy:
        return !this.modalNtfyServerUrl().trim() || this.modalNtfyTopics().length === 0;
      case NotificationProviderType.Pushover:
        return !this.modalPushoverApiToken().trim() || !this.modalPushoverUserKey().trim();
    }
    return false;
  });

  // Per-provider field errors
  readonly discordWebhookError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Discord) return undefined;
    if (!this.modalWebhookUrl().trim()) return 'Webhook URL is required';
    return undefined;
  });
  readonly telegramBotTokenError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Telegram) return undefined;
    if (!this.modalBotToken().trim()) return 'Bot token is required';
    return undefined;
  });
  readonly telegramChatIdError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Telegram) return undefined;
    if (!this.modalChatId().trim()) return 'Chat ID is required';
    return undefined;
  });
  readonly notifiarrApiKeyError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Notifiarr) return undefined;
    if (!this.modalApiKey().trim()) return 'API key is required';
    return undefined;
  });
  readonly appriseUrlError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Apprise) return undefined;
    if ((this.modalAppriseMode() as AppriseMode) === AppriseMode.Api && !this.modalAppriseUrl().trim()) return 'Server URL is required';
    return undefined;
  });
  readonly appriseKeyError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Apprise) return undefined;
    if ((this.modalAppriseMode() as AppriseMode) === AppriseMode.Api && !this.modalAppriseKey().trim()) return 'Config key is required';
    return undefined;
  });
  readonly appriseServiceUrlsError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Apprise) return undefined;
    if ((this.modalAppriseMode() as AppriseMode) === AppriseMode.Cli && this.modalAppriseServiceUrls().length === 0) return 'At least one service URL is required';
    return undefined;
  });
  readonly ntfyServerUrlError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Ntfy) return undefined;
    if (!this.modalNtfyServerUrl().trim()) return 'Server URL is required';
    return undefined;
  });
  readonly ntfyTopicsError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Ntfy) return undefined;
    if (this.modalNtfyTopics().length === 0) return 'At least one topic is required';
    return undefined;
  });
  readonly pushoverApiTokenError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Pushover) return undefined;
    if (!this.modalPushoverApiToken().trim()) return 'API token is required';
    return undefined;
  });
  readonly pushoverUserKeyError = computed(() => {
    if (this.modalType() !== NotificationProviderType.Pushover) return undefined;
    if (!this.modalPushoverUserKey().trim()) return 'User key is required';
    return undefined;
  });

  // Options (exposed for template)
  readonly appriseOptions = APPRISE_MODE_OPTIONS;
  readonly ntfyAuthOptions = NTFY_AUTH_OPTIONS;
  readonly ntfyPriorityOptions = NTFY_PRIORITY_OPTIONS;
  readonly pushoverPriorityOptions = PUSHOVER_PRIORITY_OPTIONS;
  readonly pushoverSoundOptions = PUSHOVER_SOUND_OPTIONS;

  // Provider selection data
  readonly availableProviders = [
    { type: NotificationProviderType.Apprise, name: 'Apprise', iconUrl: 'icons/ext/apprise.svg', iconLightUrl: 'icons/ext/apprise-light.svg', description: 'github.com/caronc/apprise' },
    { type: NotificationProviderType.Discord, name: 'Discord', iconUrl: 'icons/ext/discord.svg', iconLightUrl: 'icons/ext/discord-light.svg', description: 'discord.com' },
    { type: NotificationProviderType.Notifiarr, name: 'Notifiarr', iconUrl: 'icons/ext/notifiarr.svg', iconLightUrl: 'icons/ext/notifiarr-light.svg', description: 'notifiarr.com' },
    { type: NotificationProviderType.Ntfy, name: 'ntfy', iconUrl: 'icons/ext/ntfy.svg', iconLightUrl: 'icons/ext/ntfy-light.svg', description: 'ntfy.sh' },
    { type: NotificationProviderType.Pushover, name: 'Pushover', iconUrl: 'icons/ext/pushover.svg', iconLightUrl: 'icons/ext/pushover-light.svg', description: 'pushover.net' },
    { type: NotificationProviderType.Telegram, name: 'Telegram', iconUrl: 'icons/ext/telegram.svg', iconLightUrl: 'icons/ext/telegram-light.svg', description: 'core.telegram.org/bots' },
  ];

  ngOnInit(): void {
    this.loadProviders();
  }

  private loadProviders(): void {
    this.loader.start();
    this.api.getProviders().subscribe({
      next: (config) => {
        this.providers.set(config.providers ?? []);
        this.loader.stop();
      },
      error: () => {
        this.toast.error('Failed to load notification providers');
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadProviders();
  }

  openAddModal(): void {
    this.selectionModalVisible.set(true);
  }

  onProviderTypeSelected(type: NotificationProviderType): void {
    this.selectionModalVisible.set(false);
    this.editingProvider.set(null);
    this.modalType.set(type);
    this.modalName.set('');
    this.modalEnabled.set(true);
    this.resetModalFields();
    this.resetEventFlags();
    this.modalVisible.set(true);
  }

  openEditModal(provider: NotificationProviderDto): void {
    this.editingProvider.set(provider);
    this.modalType.set(provider.type);
    this.modalName.set(provider.name);
    this.modalEnabled.set(provider.isEnabled);
    this.resetModalFields();

    const config = provider.configuration as any;
    switch (provider.type) {
      case NotificationProviderType.Discord:
        this.modalWebhookUrl.set(config.webhookUrl ?? '');
        this.modalUsername.set(config.username ?? '');
        this.modalAvatarUrl.set(config.avatarUrl ?? '');
        break;
      case NotificationProviderType.Telegram:
        this.modalBotToken.set(config.botToken ?? '');
        this.modalChatId.set(config.chatId ?? '');
        this.modalTopicId.set(config.topicId ?? '');
        this.modalSendSilently.set(config.sendSilently ?? false);
        break;
      case NotificationProviderType.Notifiarr:
        this.modalApiKey.set(config.apiKey ?? '');
        this.modalChannelId.set(config.channelId ?? '');
        break;
      case NotificationProviderType.Apprise:
        this.modalAppriseMode.set(config.mode ?? AppriseMode.Api);
        this.modalAppriseUrl.set(config.url ?? '');
        this.modalAppriseKey.set(config.key ?? '');
        this.modalAppriseTags.set(config.tags ?? '');
        this.modalAppriseServiceUrls.set(config.serviceUrls ? config.serviceUrls.split('\n').filter((s: string) => s.trim()) : []);
        break;
      case NotificationProviderType.Ntfy:
        this.modalNtfyServerUrl.set(config.serverUrl ?? 'https://ntfy.sh');
        this.modalNtfyTopics.set(config.topics ?? []);
        this.modalNtfyAuthType.set(config.authenticationType ?? NtfyAuthenticationType.None);
        this.modalNtfyUsername.set(config.username ?? '');
        this.modalNtfyPassword.set(config.password ?? '');
        this.modalNtfyAccessToken.set(config.accessToken ?? '');
        this.modalNtfyPriority.set(config.priority ?? NtfyPriority.Default);
        this.modalNtfyTags.set(config.tags ?? []);
        break;
      case NotificationProviderType.Pushover:
        this.modalPushoverApiToken.set(config.apiToken ?? '');
        this.modalPushoverUserKey.set(config.userKey ?? '');
        this.modalPushoverDevices.set(config.devices ?? []);
        this.modalPushoverPriority.set(config.priority ?? PushoverPriority.Normal);
        this.modalPushoverRetry.set(config.retry ?? 30);
        this.modalPushoverExpire.set(config.expire ?? 3600);
        this.modalPushoverSound.set(config.sound ?? '');
        this.modalPushoverCustomSound.set(config.customSound ?? '');
        this.modalPushoverTags.set(config.tags ?? []);
        break;
    }

    this.onFailedImportStrike.set(provider.events.onFailedImportStrike);
    this.onStalledStrike.set(provider.events.onStalledStrike);
    this.onSlowStrike.set(provider.events.onSlowStrike);
    this.onQueueItemDeleted.set(provider.events.onQueueItemDeleted);
    this.onDownloadCleaned.set(provider.events.onDownloadCleaned);
    this.onCategoryChanged.set(provider.events.onCategoryChanged);
    this.modalVisible.set(true);
  }

  private resetModalFields(): void {
    // Discord
    this.modalWebhookUrl.set('');
    this.modalUsername.set('');
    this.modalAvatarUrl.set('');
    // Telegram
    this.modalBotToken.set('');
    this.modalChatId.set('');
    this.modalTopicId.set('');
    this.modalSendSilently.set(false);
    // Notifiarr
    this.modalApiKey.set('');
    this.modalChannelId.set('');
    // Apprise
    this.modalAppriseMode.set(AppriseMode.Api);
    this.modalAppriseUrl.set('');
    this.modalAppriseKey.set('');
    this.modalAppriseTags.set('');
    this.modalAppriseServiceUrls.set([]);
    // Ntfy
    this.modalNtfyServerUrl.set('https://ntfy.sh');
    this.modalNtfyTopics.set([]);
    this.modalNtfyAuthType.set(NtfyAuthenticationType.None);
    this.modalNtfyUsername.set('');
    this.modalNtfyPassword.set('');
    this.modalNtfyAccessToken.set('');
    this.modalNtfyPriority.set(NtfyPriority.Default);
    this.modalNtfyTags.set([]);
    // Pushover
    this.modalPushoverApiToken.set('');
    this.modalPushoverUserKey.set('');
    this.modalPushoverDevices.set([]);
    this.modalPushoverPriority.set(PushoverPriority.Normal);
    this.modalPushoverRetry.set(30);
    this.modalPushoverExpire.set(3600);
    this.modalPushoverSound.set('');
    this.modalPushoverCustomSound.set('');
    this.modalPushoverTags.set([]);
  }

  private resetEventFlags(): void {
    this.onFailedImportStrike.set(true);
    this.onStalledStrike.set(true);
    this.onSlowStrike.set(true);
    this.onQueueItemDeleted.set(true);
    this.onDownloadCleaned.set(true);
    this.onCategoryChanged.set(false);
  }

  private getEventFlags() {
    return {
      onFailedImportStrike: this.onFailedImportStrike(),
      onStalledStrike: this.onStalledStrike(),
      onSlowStrike: this.onSlowStrike(),
      onQueueItemDeleted: this.onQueueItemDeleted(),
      onDownloadCleaned: this.onDownloadCleaned(),
      onCategoryChanged: this.onCategoryChanged(),
    };
  }

  testNotification(): void {
    const type = this.modalType();
    this.testing.set(true);

    switch (type) {
      case NotificationProviderType.Discord:
        this.api.testDiscord({
          webhookUrl: this.modalWebhookUrl(),
          username: this.modalUsername() || undefined,
          avatarUrl: this.modalAvatarUrl() || undefined,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Telegram:
        this.api.testTelegram({
          botToken: this.modalBotToken(),
          chatId: this.modalChatId(),
          topicId: this.modalTopicId() || undefined,
          sendSilently: this.modalSendSilently(),
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Notifiarr:
        this.api.testNotifiarr({
          apiKey: this.modalApiKey(),
          channelId: this.modalChannelId(),
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Apprise:
        this.api.testApprise({
          mode: this.modalAppriseMode() as AppriseMode,
          url: this.modalAppriseUrl() || undefined,
          key: this.modalAppriseKey() || undefined,
          tags: this.modalAppriseTags() || undefined,
          serviceUrls: this.modalAppriseServiceUrls().join('\n') || undefined,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Ntfy:
        this.api.testNtfy({
          serverUrl: this.modalNtfyServerUrl(),
          topics: this.modalNtfyTopics(),
          authenticationType: this.modalNtfyAuthType() as NtfyAuthenticationType,
          username: this.modalNtfyUsername() || undefined,
          password: this.modalNtfyPassword() || undefined,
          accessToken: this.modalNtfyAccessToken() || undefined,
          priority: this.modalNtfyPriority() as NtfyPriority,
          tags: this.modalNtfyTags().length > 0 ? this.modalNtfyTags() : undefined,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      case NotificationProviderType.Pushover: {
        const sound = this.modalPushoverSound() as string;
        this.api.testPushover({
          apiToken: this.modalPushoverApiToken(),
          userKey: this.modalPushoverUserKey(),
          devices: this.modalPushoverDevices().length > 0 ? this.modalPushoverDevices() : undefined,
          priority: this.modalPushoverPriority() as PushoverPriority,
          sound: sound === '__custom__' ? this.modalPushoverCustomSound() : (sound || undefined),
          retry: this.modalPushoverPriority() === PushoverPriority.Emergency ? (this.modalPushoverRetry() ?? 30) : undefined,
          expire: this.modalPushoverPriority() === PushoverPriority.Emergency ? (this.modalPushoverExpire() ?? 3600) : undefined,
          tags: this.modalPushoverTags().length > 0 ? this.modalPushoverTags() : undefined,
        }).subscribe({
          next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
          error: () => { this.toast.error('Test failed'); this.testing.set(false); },
        });
        break;
      }
    }
  }

  saveProvider(): void {
    if (this.hasModalErrors()) return;
    const type = this.modalType();
    const editing = this.editingProvider();
    this.saving.set(true);
    const eventFlags = this.getEventFlags();

    switch (type) {
      case NotificationProviderType.Discord: {
        const request: CreateDiscordProviderRequest = {
          name: this.modalName(),
          webhookUrl: this.modalWebhookUrl(),
          username: this.modalUsername() || undefined,
          avatarUrl: this.modalAvatarUrl() || undefined,
          isEnabled: this.modalEnabled(),
          ...eventFlags,
        };
        const obs = editing ? this.api.updateDiscord(editing.id, request) : this.api.createDiscord(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Telegram: {
        const request: CreateTelegramProviderRequest = {
          name: this.modalName(),
          botToken: this.modalBotToken(),
          chatId: this.modalChatId(),
          topicId: this.modalTopicId() || undefined,
          sendSilently: this.modalSendSilently(),
          isEnabled: this.modalEnabled(),
          ...eventFlags,
        };
        const obs = editing ? this.api.updateTelegram(editing.id, request) : this.api.createTelegram(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Notifiarr: {
        const request: CreateNotifiarrProviderRequest = {
          name: this.modalName(),
          apiKey: this.modalApiKey(),
          channelId: this.modalChannelId(),
          isEnabled: this.modalEnabled(),
          ...eventFlags,
        };
        const obs = editing ? this.api.updateNotifiarr(editing.id, request) : this.api.createNotifiarr(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Apprise: {
        const request: CreateAppriseProviderRequest = {
          name: this.modalName(),
          mode: this.modalAppriseMode() as AppriseMode,
          url: this.modalAppriseUrl() || undefined,
          key: this.modalAppriseKey() || undefined,
          tags: this.modalAppriseTags() || undefined,
          serviceUrls: this.modalAppriseServiceUrls().join('\n') || undefined,
          isEnabled: this.modalEnabled(),
          ...eventFlags,
        };
        const obs = editing ? this.api.updateApprise(editing.id, request) : this.api.createApprise(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Ntfy: {
        const request: CreateNtfyProviderRequest = {
          name: this.modalName(),
          serverUrl: this.modalNtfyServerUrl(),
          topics: this.modalNtfyTopics(),
          authenticationType: this.modalNtfyAuthType() as NtfyAuthenticationType,
          username: this.modalNtfyUsername() || undefined,
          password: this.modalNtfyPassword() || undefined,
          accessToken: this.modalNtfyAccessToken() || undefined,
          priority: this.modalNtfyPriority() as NtfyPriority,
          tags: this.modalNtfyTags().length > 0 ? this.modalNtfyTags() : undefined,
          isEnabled: this.modalEnabled(),
          ...eventFlags,
        };
        const obs = editing ? this.api.updateNtfy(editing.id, request) : this.api.createNtfy(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
      case NotificationProviderType.Pushover: {
        const sound = this.modalPushoverSound() as string;
        const request: CreatePushoverProviderRequest = {
          name: this.modalName(),
          apiToken: this.modalPushoverApiToken(),
          userKey: this.modalPushoverUserKey(),
          devices: this.modalPushoverDevices().length > 0 ? this.modalPushoverDevices() : undefined,
          priority: this.modalPushoverPriority() as PushoverPriority,
          sound: sound === '__custom__' ? this.modalPushoverCustomSound() : (sound || undefined),
          retry: this.modalPushoverPriority() === PushoverPriority.Emergency ? (this.modalPushoverRetry() ?? 30) : undefined,
          expire: this.modalPushoverPriority() === PushoverPriority.Emergency ? (this.modalPushoverExpire() ?? 3600) : undefined,
          tags: this.modalPushoverTags().length > 0 ? this.modalPushoverTags() : undefined,
          isEnabled: this.modalEnabled(),
          ...eventFlags,
        };
        const obs = editing ? this.api.updatePushover(editing.id, request) : this.api.createPushover(request);
        obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
        break;
      }
    }
  }

  private onSaveSuccess(editing: NotificationProviderDto | null): void {
    this.toast.success(editing ? 'Provider updated' : 'Provider added');
    this.modalVisible.set(false);
    this.saving.set(false);
    this.loadProviders();
  }

  private onSaveError(): void {
    this.toast.error('Failed to save provider');
    this.saving.set(false);
  }

  async deleteProvider(provider: NotificationProviderDto): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Provider',
      message: `Are you sure you want to delete "${provider.name}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed) return;

    this.api.deleteProvider(provider.id).subscribe({
      next: () => {
        this.toast.success('Provider deleted');
        this.loadProviders();
      },
      error: () => this.toast.error('Failed to delete provider'),
    });
  }

  hasPendingChanges(): boolean {
    return false;
  }
}
