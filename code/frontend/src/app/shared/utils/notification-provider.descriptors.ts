import {
  NotificationEventFlags,
  CreateDiscordProviderRequest,
  CreateTelegramProviderRequest,
  CreateNotifiarrProviderRequest,
  CreateAppriseProviderRequest,
  CreateNtfyProviderRequest,
  CreatePushoverProviderRequest,
  CreateGotifyProviderRequest,
  TestDiscordRequest,
  TestTelegramRequest,
  TestNotifiarrRequest,
  TestAppriseRequest,
  TestNtfyRequest,
  TestPushoverRequest,
  TestGotifyRequest,
} from '@shared/models/notification-provider.model';
import {
  NotificationProviderType,
  AppriseMode,
  NtfyAuthenticationType,
  NtfyPriority,
  PushoverPriority,
} from '@shared/models/enums';

/** Flat form model covering every provider's fields, mirroring the notification provider modal's model. */
export interface NotificationProviderFormModel extends NotificationEventFlags {
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
}

function parseGotifyPriority(value: string): number {
  const priority = Number.parseInt(value, 10);
  return Number.isNaN(priority) ? 5 : priority;
}

function eventFlags(m: NotificationProviderFormModel): NotificationEventFlags {
  return {
    onFailedImportStrike: m.onFailedImportStrike,
    onStalledStrike: m.onStalledStrike,
    onSlowStrike: m.onSlowStrike,
    onQueueItemDeleted: m.onQueueItemDeleted,
    onDownloadCleaned: m.onDownloadCleaned,
    onDownloadStopped: m.onDownloadStopped,
    onCategoryChanged: m.onCategoryChanged,
    onSearchTriggered: m.onSearchTriggered,
    onSearchItemGrabbed: m.onSearchItemGrabbed,
    onForceImported: m.onForceImported,
  };
}

/**
 * Per-provider shape: the `/notification_providers/{urlSegment}` API segment,
 * how to build its create/update and test requests from the flat form model,
 * and which fields are required given the model's current values.
 */
export interface NotificationProviderDescriptor<TRequest, TTestRequest> {
  urlSegment: string;
  buildRequest(m: NotificationProviderFormModel): TRequest;
  buildTestRequest(m: NotificationProviderFormModel, providerId?: string): TTestRequest;
  requiredFields(m: NotificationProviderFormModel): (keyof NotificationProviderFormModel)[];
}

export const NOTIFICATION_PROVIDER_DESCRIPTORS: {
  [NotificationProviderType.Discord]: NotificationProviderDescriptor<CreateDiscordProviderRequest, TestDiscordRequest>;
  [NotificationProviderType.Telegram]: NotificationProviderDescriptor<CreateTelegramProviderRequest, TestTelegramRequest>;
  [NotificationProviderType.Notifiarr]: NotificationProviderDescriptor<CreateNotifiarrProviderRequest, TestNotifiarrRequest>;
  [NotificationProviderType.Apprise]: NotificationProviderDescriptor<CreateAppriseProviderRequest, TestAppriseRequest>;
  [NotificationProviderType.Ntfy]: NotificationProviderDescriptor<CreateNtfyProviderRequest, TestNtfyRequest>;
  [NotificationProviderType.Pushover]: NotificationProviderDescriptor<CreatePushoverProviderRequest, TestPushoverRequest>;
  [NotificationProviderType.Gotify]: NotificationProviderDescriptor<CreateGotifyProviderRequest, TestGotifyRequest>;
} = {
  [NotificationProviderType.Discord]: {
    urlSegment: 'discord',
    buildRequest: (m) => ({
      name: m.name,
      webhookUrl: m.webhookUrl,
      username: m.username || undefined,
      avatarUrl: m.avatarUrl || undefined,
      isEnabled: m.enabled,
      ...eventFlags(m),
    }),
    buildTestRequest: (m, providerId) => ({
      webhookUrl: m.webhookUrl,
      username: m.username || undefined,
      avatarUrl: m.avatarUrl || undefined,
      providerId,
    }),
    requiredFields: () => ['webhookUrl'],
  },
  [NotificationProviderType.Telegram]: {
    urlSegment: 'telegram',
    buildRequest: (m) => ({
      name: m.name,
      botToken: m.botToken,
      chatId: m.chatId,
      topicId: m.topicId || undefined,
      sendSilently: m.sendSilently,
      isEnabled: m.enabled,
      ...eventFlags(m),
    }),
    buildTestRequest: (m, providerId) => ({
      botToken: m.botToken,
      chatId: m.chatId,
      topicId: m.topicId || undefined,
      sendSilently: m.sendSilently,
      providerId,
    }),
    requiredFields: () => ['botToken', 'chatId'],
  },
  [NotificationProviderType.Notifiarr]: {
    urlSegment: 'notifiarr',
    buildRequest: (m) => ({
      name: m.name,
      apiKey: m.apiKey,
      channelId: m.channelId,
      isEnabled: m.enabled,
      ...eventFlags(m),
    }),
    buildTestRequest: (m, providerId) => ({
      apiKey: m.apiKey,
      channelId: m.channelId,
      providerId,
    }),
    requiredFields: () => ['apiKey'],
  },
  [NotificationProviderType.Apprise]: {
    urlSegment: 'apprise',
    buildRequest: (m) => ({
      name: m.name,
      mode: m.appriseMode,
      url: m.appriseUrl || undefined,
      key: m.appriseKey || undefined,
      tags: m.appriseTags || undefined,
      serviceUrls: m.appriseServiceUrls.join('\n') || undefined,
      isEnabled: m.enabled,
      ...eventFlags(m),
    }),
    buildTestRequest: (m, providerId) => ({
      mode: m.appriseMode,
      url: m.appriseUrl || undefined,
      key: m.appriseKey || undefined,
      tags: m.appriseTags || undefined,
      serviceUrls: m.appriseServiceUrls.join('\n') || undefined,
      providerId,
    }),
    // API mode needs a server URL + config key; CLI mode needs at least one service URL instead.
    requiredFields: (m) => (m.appriseMode === AppriseMode.Cli
      ? ['appriseServiceUrls']
      : ['appriseUrl', 'appriseKey']),
  },
  [NotificationProviderType.Ntfy]: {
    urlSegment: 'ntfy',
    buildRequest: (m) => ({
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
      ...eventFlags(m),
    }),
    buildTestRequest: (m, providerId) => ({
      serverUrl: m.ntfyServerUrl,
      topics: m.ntfyTopics,
      authenticationType: m.ntfyAuthType,
      username: m.ntfyUsername || undefined,
      password: m.ntfyPassword || undefined,
      accessToken: m.ntfyAccessToken || undefined,
      priority: m.ntfyPriority,
      tags: m.ntfyTags.length > 0 ? m.ntfyTags : undefined,
      providerId,
    }),
    // Auth fields only apply for the selected authentication type.
    requiredFields: (m) => {
      const fields: (keyof NotificationProviderFormModel)[] = ['ntfyServerUrl', 'ntfyTopics'];
      if (m.ntfyAuthType === NtfyAuthenticationType.BasicAuth) {
        fields.push('ntfyUsername', 'ntfyPassword');
      } else if (m.ntfyAuthType === NtfyAuthenticationType.AccessToken) {
        fields.push('ntfyAccessToken');
      }
      return fields;
    },
  },
  [NotificationProviderType.Pushover]: {
    urlSegment: 'pushover',
    buildRequest: (m) => ({
      name: m.name,
      apiToken: m.pushoverApiToken,
      userKey: m.pushoverUserKey,
      devices: m.pushoverDevices.length > 0 ? m.pushoverDevices : undefined,
      priority: m.pushoverPriority,
      sound: m.pushoverSound === '__custom__' ? m.pushoverCustomSound : (m.pushoverSound || undefined),
      retry: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverRetry ?? 30) : undefined,
      expire: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverExpire ?? 3600) : undefined,
      tags: m.pushoverTags.length > 0 ? m.pushoverTags : undefined,
      isEnabled: m.enabled,
      ...eventFlags(m),
    }),
    buildTestRequest: (m, providerId) => ({
      apiToken: m.pushoverApiToken,
      userKey: m.pushoverUserKey,
      devices: m.pushoverDevices.length > 0 ? m.pushoverDevices : undefined,
      priority: m.pushoverPriority,
      sound: m.pushoverSound === '__custom__' ? m.pushoverCustomSound : (m.pushoverSound || undefined),
      retry: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverRetry ?? 30) : undefined,
      expire: m.pushoverPriority === PushoverPriority.Emergency ? (m.pushoverExpire ?? 3600) : undefined,
      tags: m.pushoverTags.length > 0 ? m.pushoverTags : undefined,
      providerId,
    }),
    // Retry/expire are only meaningful (and only bounded) at Emergency priority.
    requiredFields: (m) => (m.pushoverPriority === PushoverPriority.Emergency
      ? ['pushoverApiToken', 'pushoverUserKey', 'pushoverRetry', 'pushoverExpire']
      : ['pushoverApiToken', 'pushoverUserKey']),
  },
  [NotificationProviderType.Gotify]: {
    urlSegment: 'gotify',
    buildRequest: (m) => ({
      name: m.name,
      serverUrl: m.gotifyServerUrl,
      applicationToken: m.gotifyApplicationToken,
      priority: parseGotifyPriority(m.gotifyPriority),
      isEnabled: m.enabled,
      ...eventFlags(m),
    }),
    buildTestRequest: (m, providerId) => ({
      serverUrl: m.gotifyServerUrl,
      applicationToken: m.gotifyApplicationToken,
      priority: parseGotifyPriority(m.gotifyPriority),
      providerId,
    }),
    requiredFields: () => ['gotifyServerUrl', 'gotifyApplicationToken'],
  },
};
