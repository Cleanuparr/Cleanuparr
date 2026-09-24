import { Component, ChangeDetectionStrategy, inject, signal, computed, input, model, output, effect, untracked } from '@angular/core';
import { form, validate, FormField } from '@angular/forms/signals';
import {
  ButtonComponent, InputComponent, ToggleComponent, SelectComponent,
  ModalComponent, ChipInputComponent, NumberInputComponent, BadgeComponent,
  type SelectOption,
} from '@ui';
import { NotificationApi } from '@core/api/notification.api';
import { ToastService } from '@core/services/toast.service';
import { NotificationProviderDto, AppriseCliStatus } from '@shared/models/notification-provider.model';
import {
  NotificationProviderType,
  AppriseMode,
  NtfyAuthenticationType,
  NtfyPriority,
  PushoverPriority,
} from '@shared/models/enums';
import { NOTIFICATION_PROVIDER_DESCRIPTORS, NotificationProviderFormModel } from '@shared/utils/notification-provider.descriptors';

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

function createDefaultModalModel(): NotificationProviderFormModel {
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
    onFailedImportStrike: false, onStalledStrike: false, onSlowStrike: false, onQueueItemDeleted: false,
    onDownloadCleaned: false, onDownloadStopped: false, onCategoryChanged: false, onSearchTriggered: false, onSearchItemGrabbed: false, onForceImported: false,
  };
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
    ModalComponent, ChipInputComponent, NumberInputComponent, FormField, BadgeComponent,
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

  readonly checkingAppriseCli = signal(false);
  readonly appriseCliStatus = signal<AppriseCliStatus | null>(null);
  private appriseCliChecked = false;

  readonly modalModel = signal<NotificationProviderFormModel>(createDefaultModalModel());

  /** JSON snapshot of the model as loaded when the modal opened, for dirty tracking. */
  private readonly openSnapshot = signal('');
  readonly hasPendingChanges = computed(() =>
    this.visible() && JSON.stringify(this.modalModel()) !== this.openSnapshot());

  /** A required-field validator body driven by the active provider's descriptor. */
  private requiredError(field: keyof NotificationProviderFormModel, message: string) {
    return () => {
      const m = this.modalModel();
      if (!NOTIFICATION_PROVIDER_DESCRIPTORS[this.modalType()].requiredFields(m).includes(field)) {
        return undefined;
      }
      const value = m[field];
      const empty = Array.isArray(value) ? value.length === 0 : !String(value ?? '').trim();
      return empty ? { kind: 'required', message } : undefined;
    };
  }

  readonly modalForm = form(this.modalModel, (p) => {
    validate(p.name, () =>
      !this.modalModel().name.trim() ? { kind: 'required', message: 'Name is required' } : undefined);

    validate(p.webhookUrl, this.requiredError('webhookUrl', 'Webhook URL is required'));
    validate(p.botToken, this.requiredError('botToken', 'Bot token is required'));
    validate(p.chatId, this.requiredError('chatId', 'Chat ID is required'));
    validate(p.apiKey, this.requiredError('apiKey', 'API key is required'));
    validate(p.appriseUrl, this.requiredError('appriseUrl', 'Server URL is required'));
    validate(p.appriseKey, this.requiredError('appriseKey', 'Config key is required'));
    validate(p.ntfyServerUrl, this.requiredError('ntfyServerUrl', 'Server URL is required'));
    validate(p.ntfyTopics, this.requiredError('ntfyTopics', 'At least one topic is required'));
    validate(p.ntfyUsername, this.requiredError('ntfyUsername', 'Username is required'));
    validate(p.ntfyPassword, this.requiredError('ntfyPassword', 'Password is required'));
    validate(p.ntfyAccessToken, this.requiredError('ntfyAccessToken', 'Access token is required'));
    validate(p.pushoverApiToken, this.requiredError('pushoverApiToken', 'API token is required'));
    validate(p.pushoverUserKey, this.requiredError('pushoverUserKey', 'User key is required'));
    validate(p.gotifyServerUrl, this.requiredError('gotifyServerUrl', 'Server URL is required'));
    validate(p.gotifyApplicationToken, this.requiredError('gotifyApplicationToken', 'Application token is required'));

    // Apprise: CLI mode needs at least one service URL instead of a server URL + config key.
    validate(p.appriseServiceUrls, () =>
      this.modalType() === NotificationProviderType.Apprise && this.modalModel().appriseMode === AppriseMode.Cli && this.modalModel().appriseServiceUrls.length === 0
        ? { kind: 'required', message: 'At least one service URL is required' } : undefined);

    // Pushover: retry/expire only apply (and are only bounded) at Emergency priority; skip
    // otherwise so stale values from a hidden field can't keep the modal Save disabled.
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
        const next = provider ? this.buildModelFromProvider(provider) : createDefaultModalModel();
        this.modalType.set(provider ? provider.type : initialType);
        this.modalModel.set(next);
        this.openSnapshot.set(JSON.stringify(next));
        this.appriseCliChecked = false;
        this.appriseCliStatus.set(null);
      });
    });

    effect(() => {
      const isAppriseCli = this.modalType() === NotificationProviderType.Apprise
        && this.modalModel().appriseMode === AppriseMode.Cli;
      if (!this.visible() || !isAppriseCli) {
        return;
      }
      untracked(() => this.checkAppriseCli());
    });
  }

  private checkAppriseCli(): void {
    if (this.appriseCliChecked) {
      return;
    }
    this.appriseCliChecked = true;
    this.checkingAppriseCli.set(true);
    this.api.getAppriseCliStatus().subscribe({
      next: (status) => {
        this.appriseCliStatus.set(status);
        this.checkingAppriseCli.set(false);
      },
      error: () => {
        this.appriseCliStatus.set({ available: false });
        this.checkingAppriseCli.set(false);
      },
    });
  }

  private buildModelFromProvider(provider: NotificationProviderDto): NotificationProviderFormModel {
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
    model.onDownloadStopped = provider.events.onDownloadStopped;
    model.onCategoryChanged = provider.events.onCategoryChanged;
    model.onSearchTriggered = provider.events.onSearchTriggered;
    model.onSearchItemGrabbed = provider.events.onSearchItemGrabbed;
    model.onForceImported = provider.events.onForceImported;

    return model;
  }

  testNotification(): void {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[this.modalType()];
    const providerId = this.editingProvider()?.id;
    this.testing.set(true);
    this.api.test(descriptor.urlSegment, descriptor.buildTestRequest(this.modalModel(), providerId)).subscribe({
      next: (r) => { this.toast.success(r.message || 'Test sent'); this.testing.set(false); },
      error: () => { this.toast.error('Test failed'); this.testing.set(false); },
    });
  }

  saveProvider(): void {
    if (this.modalForm().invalid()) return;
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[this.modalType()];
    const editing = this.editingProvider();
    const request = descriptor.buildRequest(this.modalModel());
    this.saving.set(true);
    const obs = editing
      ? this.api.update(descriptor.urlSegment, editing.id, request)
      : this.api.create(descriptor.urlSegment, request);
    obs.subscribe({ next: () => this.onSaveSuccess(editing), error: () => this.onSaveError() });
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
