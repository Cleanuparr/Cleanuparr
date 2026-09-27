import { NOTIFICATION_PROVIDER_DESCRIPTORS, NotificationProviderFormModel } from './notification-provider.descriptors';
import {
  NotificationProviderType,
  AppriseMode,
  NtfyAuthenticationType,
  NtfyPriority,
  PushoverPriority,
} from '@shared/models/enums';

function baseModel(overrides: Partial<NotificationProviderFormModel> = {}): NotificationProviderFormModel {
  return {
    name: 'my provider',
    enabled: true,
    webhookUrl: '',
    username: '',
    avatarUrl: '',
    botToken: '',
    chatId: '',
    topicId: '',
    sendSilently: false,
    apiKey: '',
    channelId: '',
    appriseMode: AppriseMode.Api,
    appriseUrl: '',
    appriseKey: '',
    appriseTags: '',
    appriseServiceUrls: [],
    ntfyServerUrl: 'https://ntfy.sh',
    ntfyTopics: [],
    ntfyAuthType: NtfyAuthenticationType.None,
    ntfyUsername: '',
    ntfyPassword: '',
    ntfyAccessToken: '',
    ntfyPriority: NtfyPriority.Default,
    ntfyTags: [],
    gotifyServerUrl: '',
    gotifyApplicationToken: '',
    gotifyPriority: '5',
    pushoverApiToken: '',
    pushoverUserKey: '',
    pushoverDevices: [],
    pushoverPriority: PushoverPriority.Normal,
    pushoverRetry: 30,
    pushoverExpire: 3600,
    pushoverSound: '',
    pushoverCustomSound: '',
    pushoverTags: [],
    onFailedImportStrike: false,
    onStalledStrike: false,
    onSlowStrike: false,
    onQueueItemDeleted: false,
    onDownloadCleaned: false,
    onDownloadStopped: false,
    onCategoryChanged: false,
    onSearchTriggered: false,
    onSearchItemGrabbed: false,
    onForceImported: false,
    ...overrides,
  };
}

const eventFlags = {
  onFailedImportStrike: false,
  onStalledStrike: false,
  onSlowStrike: false,
  onQueueItemDeleted: false,
  onDownloadCleaned: false,
  onDownloadStopped: false,
  onCategoryChanged: false,
  onSearchTriggered: false,
  onSearchItemGrabbed: false,
  onForceImported: false,
};

describe('NOTIFICATION_PROVIDER_DESCRIPTORS', () => {
  describe('Discord', () => {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[NotificationProviderType.Discord];

    it('builds a request with username and avatarUrl set', () => {
      const model = baseModel({ webhookUrl: 'https://discord/hook', username: 'bot', avatarUrl: 'https://a.png' });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        webhookUrl: 'https://discord/hook',
        username: 'bot',
        avatarUrl: 'https://a.png',
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('turns blank username and avatarUrl into undefined', () => {
      const model = baseModel({ webhookUrl: 'https://discord/hook' });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        webhookUrl: 'https://discord/hook',
        username: undefined,
        avatarUrl: undefined,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a test request with a providerId', () => {
      const model = baseModel({ webhookUrl: 'https://discord/hook', username: 'bot', avatarUrl: 'https://a.png' });

      expect(descriptor.buildTestRequest(model, 'p1')).toEqual({
        webhookUrl: 'https://discord/hook',
        username: 'bot',
        avatarUrl: 'https://a.png',
        providerId: 'p1',
      });
    });

    it('builds a test request without a providerId', () => {
      const model = baseModel({ webhookUrl: 'https://discord/hook' });

      expect(descriptor.buildTestRequest(model)).toEqual({
        webhookUrl: 'https://discord/hook',
        username: undefined,
        avatarUrl: undefined,
        providerId: undefined,
      });
    });

    it('requires only webhookUrl', () => {
      expect(descriptor.requiredFields(baseModel())).toEqual(['webhookUrl']);
    });
  });

  describe('Telegram', () => {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[NotificationProviderType.Telegram];

    it('builds a request with topicId set', () => {
      const model = baseModel({ botToken: 'token', chatId: 'chat1', topicId: 'topic1', sendSilently: true });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        botToken: 'token',
        chatId: 'chat1',
        topicId: 'topic1',
        sendSilently: true,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('turns a blank topicId into undefined', () => {
      const model = baseModel({ botToken: 'token', chatId: 'chat1' });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        botToken: 'token',
        chatId: 'chat1',
        topicId: undefined,
        sendSilently: false,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a test request with topicId set', () => {
      const model = baseModel({ botToken: 'token', chatId: 'chat1', topicId: 'topic1' });

      expect(descriptor.buildTestRequest(model, 'p1')).toEqual({
        botToken: 'token',
        chatId: 'chat1',
        topicId: 'topic1',
        sendSilently: false,
        providerId: 'p1',
      });
    });

    it('builds a test request with a blank topicId', () => {
      const model = baseModel({ botToken: 'token', chatId: 'chat1' });

      expect(descriptor.buildTestRequest(model)).toEqual({
        botToken: 'token',
        chatId: 'chat1',
        topicId: undefined,
        sendSilently: false,
        providerId: undefined,
      });
    });

    it('requires botToken and chatId', () => {
      expect(descriptor.requiredFields(baseModel())).toEqual(['botToken', 'chatId']);
    });
  });

  describe('Notifiarr', () => {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[NotificationProviderType.Notifiarr];

    it('builds a request', () => {
      const model = baseModel({ apiKey: 'key1', channelId: 'chan1' });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        apiKey: 'key1',
        channelId: 'chan1',
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a test request', () => {
      const model = baseModel({ apiKey: 'key1', channelId: 'chan1' });

      expect(descriptor.buildTestRequest(model, 'p1')).toEqual({
        apiKey: 'key1',
        channelId: 'chan1',
        providerId: 'p1',
      });
    });

    it('requires only apiKey', () => {
      expect(descriptor.requiredFields(baseModel())).toEqual(['apiKey']);
    });
  });

  describe('Apprise', () => {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[NotificationProviderType.Apprise];

    it('builds a request in API mode', () => {
      const model = baseModel({
        appriseMode: AppriseMode.Api,
        appriseUrl: 'https://apprise/notify',
        appriseKey: 'config-key',
        appriseTags: 'tag1,tag2',
        appriseServiceUrls: [],
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        mode: AppriseMode.Api,
        url: 'https://apprise/notify',
        key: 'config-key',
        tags: 'tag1,tag2',
        serviceUrls: undefined,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a request in CLI mode, joining service urls and blanking API-only fields', () => {
      const model = baseModel({
        appriseMode: AppriseMode.Cli,
        appriseServiceUrls: ['tgram://one', 'discord://two'],
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        mode: AppriseMode.Cli,
        url: undefined,
        key: undefined,
        tags: undefined,
        serviceUrls: 'tgram://one\ndiscord://two',
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a test request in API mode', () => {
      const model = baseModel({
        appriseMode: AppriseMode.Api,
        appriseUrl: 'https://apprise/notify',
        appriseKey: 'config-key',
        appriseTags: 'tag1',
      });

      expect(descriptor.buildTestRequest(model, 'p1')).toEqual({
        mode: AppriseMode.Api,
        url: 'https://apprise/notify',
        key: 'config-key',
        tags: 'tag1',
        serviceUrls: undefined,
        providerId: 'p1',
      });
    });

    it('builds a test request in CLI mode', () => {
      const model = baseModel({
        appriseMode: AppriseMode.Cli,
        appriseServiceUrls: ['tgram://one'],
      });

      expect(descriptor.buildTestRequest(model)).toEqual({
        mode: AppriseMode.Cli,
        url: undefined,
        key: undefined,
        tags: undefined,
        serviceUrls: 'tgram://one',
        providerId: undefined,
      });
    });

    it('requires appriseUrl and appriseKey in API mode', () => {
      expect(descriptor.requiredFields(baseModel({ appriseMode: AppriseMode.Api }))).toEqual(['appriseUrl', 'appriseKey']);
    });

    it('requires appriseServiceUrls in CLI mode', () => {
      expect(descriptor.requiredFields(baseModel({ appriseMode: AppriseMode.Cli }))).toEqual(['appriseServiceUrls']);
    });
  });

  describe('Ntfy', () => {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[NotificationProviderType.Ntfy];

    it('builds a request with no authentication and empty tags', () => {
      const model = baseModel({
        ntfyServerUrl: 'https://ntfy.sh',
        ntfyTopics: ['topic1'],
        ntfyAuthType: NtfyAuthenticationType.None,
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        serverUrl: 'https://ntfy.sh',
        topics: ['topic1'],
        authenticationType: NtfyAuthenticationType.None,
        username: undefined,
        password: undefined,
        accessToken: undefined,
        priority: NtfyPriority.Default,
        tags: undefined,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a request with basic auth and non-empty tags', () => {
      const model = baseModel({
        ntfyServerUrl: 'https://ntfy.sh',
        ntfyTopics: ['topic1'],
        ntfyAuthType: NtfyAuthenticationType.BasicAuth,
        ntfyUsername: 'user1',
        ntfyPassword: 'pass1',
        ntfyTags: ['warning'],
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        serverUrl: 'https://ntfy.sh',
        topics: ['topic1'],
        authenticationType: NtfyAuthenticationType.BasicAuth,
        username: 'user1',
        password: 'pass1',
        accessToken: undefined,
        priority: NtfyPriority.Default,
        tags: ['warning'],
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a request with access token auth', () => {
      const model = baseModel({
        ntfyServerUrl: 'https://ntfy.sh',
        ntfyTopics: ['topic1'],
        ntfyAuthType: NtfyAuthenticationType.AccessToken,
        ntfyAccessToken: 'tok123',
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        serverUrl: 'https://ntfy.sh',
        topics: ['topic1'],
        authenticationType: NtfyAuthenticationType.AccessToken,
        username: undefined,
        password: undefined,
        accessToken: 'tok123',
        priority: NtfyPriority.Default,
        tags: undefined,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('builds a test request with basic auth and tags', () => {
      const model = baseModel({
        ntfyServerUrl: 'https://ntfy.sh',
        ntfyTopics: ['topic1'],
        ntfyAuthType: NtfyAuthenticationType.BasicAuth,
        ntfyUsername: 'user1',
        ntfyPassword: 'pass1',
        ntfyTags: ['warning'],
      });

      expect(descriptor.buildTestRequest(model, 'p1')).toEqual({
        serverUrl: 'https://ntfy.sh',
        topics: ['topic1'],
        authenticationType: NtfyAuthenticationType.BasicAuth,
        username: 'user1',
        password: 'pass1',
        accessToken: undefined,
        priority: NtfyPriority.Default,
        tags: ['warning'],
        providerId: 'p1',
      });
    });

    it('builds a test request with no authentication and empty tags', () => {
      const model = baseModel({
        ntfyServerUrl: 'https://ntfy.sh',
        ntfyTopics: ['topic1'],
        ntfyAuthType: NtfyAuthenticationType.None,
      });

      expect(descriptor.buildTestRequest(model)).toEqual({
        serverUrl: 'https://ntfy.sh',
        topics: ['topic1'],
        authenticationType: NtfyAuthenticationType.None,
        username: undefined,
        password: undefined,
        accessToken: undefined,
        priority: NtfyPriority.Default,
        tags: undefined,
        providerId: undefined,
      });
    });

    it('requires only serverUrl and topics when auth type is none', () => {
      expect(descriptor.requiredFields(baseModel({ ntfyAuthType: NtfyAuthenticationType.None }))).toEqual(['ntfyServerUrl', 'ntfyTopics']);
    });

    it('adds username and password when auth type is basic auth', () => {
      expect(descriptor.requiredFields(baseModel({ ntfyAuthType: NtfyAuthenticationType.BasicAuth }))).toEqual([
        'ntfyServerUrl',
        'ntfyTopics',
        'ntfyUsername',
        'ntfyPassword',
      ]);
    });

    it('adds accessToken when auth type is access token', () => {
      expect(descriptor.requiredFields(baseModel({ ntfyAuthType: NtfyAuthenticationType.AccessToken }))).toEqual([
        'ntfyServerUrl',
        'ntfyTopics',
        'ntfyAccessToken',
      ]);
    });
  });

  describe('Pushover', () => {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[NotificationProviderType.Pushover];

    it('builds a request at Emergency priority with retry and expire set', () => {
      const model = baseModel({
        pushoverApiToken: 'token1',
        pushoverUserKey: 'key1',
        pushoverPriority: PushoverPriority.Emergency,
        pushoverRetry: 60,
        pushoverExpire: 7200,
        pushoverDevices: ['phone'],
        pushoverTags: ['alert'],
        pushoverSound: 'siren',
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        apiToken: 'token1',
        userKey: 'key1',
        devices: ['phone'],
        priority: PushoverPriority.Emergency,
        sound: 'siren',
        retry: 60,
        expire: 7200,
        tags: ['alert'],
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('falls back to default retry and expire at Emergency priority when unset', () => {
      const model = baseModel({
        pushoverApiToken: 'token1',
        pushoverUserKey: 'key1',
        pushoverPriority: PushoverPriority.Emergency,
        pushoverRetry: null,
        pushoverExpire: null,
      });

      const result = descriptor.buildRequest(model);

      expect(result.retry).toBe(30);
      expect(result.expire).toBe(3600);
    });

    it('builds a request at a non-emergency priority with no retry, expire, devices or tags', () => {
      const model = baseModel({
        pushoverApiToken: 'token1',
        pushoverUserKey: 'key1',
        pushoverPriority: PushoverPriority.Normal,
        pushoverRetry: 60,
        pushoverExpire: 7200,
        pushoverDevices: [],
        pushoverTags: [],
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        apiToken: 'token1',
        userKey: 'key1',
        devices: undefined,
        priority: PushoverPriority.Normal,
        sound: undefined,
        retry: undefined,
        expire: undefined,
        tags: undefined,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('uses the custom sound when pushoverSound is the custom marker', () => {
      const model = baseModel({
        pushoverApiToken: 'token1',
        pushoverUserKey: 'key1',
        pushoverSound: '__custom__',
        pushoverCustomSound: 'my-custom-sound',
      });

      expect(descriptor.buildRequest(model).sound).toBe('my-custom-sound');
    });

    it('uses the preset sound when pushoverSound is a preset value', () => {
      const model = baseModel({
        pushoverApiToken: 'token1',
        pushoverUserKey: 'key1',
        pushoverSound: 'bike',
      });

      expect(descriptor.buildRequest(model).sound).toBe('bike');
    });

    it('builds a test request at Emergency priority with retry and expire set', () => {
      const model = baseModel({
        pushoverApiToken: 'token1',
        pushoverUserKey: 'key1',
        pushoverPriority: PushoverPriority.Emergency,
        pushoverRetry: 60,
        pushoverExpire: 7200,
        pushoverDevices: ['phone'],
        pushoverTags: ['alert'],
        pushoverSound: 'siren',
      });

      expect(descriptor.buildTestRequest(model, 'p1')).toEqual({
        apiToken: 'token1',
        userKey: 'key1',
        devices: ['phone'],
        priority: PushoverPriority.Emergency,
        sound: 'siren',
        retry: 60,
        expire: 7200,
        tags: ['alert'],
        providerId: 'p1',
      });
    });

    it('builds a test request at a non-emergency priority with no retry or expire', () => {
      const model = baseModel({
        pushoverApiToken: 'token1',
        pushoverUserKey: 'key1',
        pushoverPriority: PushoverPriority.Normal,
      });

      expect(descriptor.buildTestRequest(model)).toEqual({
        apiToken: 'token1',
        userKey: 'key1',
        devices: undefined,
        priority: PushoverPriority.Normal,
        sound: undefined,
        retry: undefined,
        expire: undefined,
        tags: undefined,
        providerId: undefined,
      });
    });

    it('requires retry and expire only at Emergency priority', () => {
      expect(descriptor.requiredFields(baseModel({ pushoverPriority: PushoverPriority.Emergency }))).toEqual([
        'pushoverApiToken',
        'pushoverUserKey',
        'pushoverRetry',
        'pushoverExpire',
      ]);
    });

    it('does not require retry and expire at non-emergency priorities', () => {
      expect(descriptor.requiredFields(baseModel({ pushoverPriority: PushoverPriority.Normal }))).toEqual([
        'pushoverApiToken',
        'pushoverUserKey',
      ]);
    });
  });

  describe('Gotify', () => {
    const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[NotificationProviderType.Gotify];

    it('builds a request, parsing a numeric priority string', () => {
      const model = baseModel({
        gotifyServerUrl: 'https://gotify.example.com',
        gotifyApplicationToken: 'apptoken1',
        gotifyPriority: '7',
      });

      expect(descriptor.buildRequest(model)).toEqual({
        name: 'my provider',
        serverUrl: 'https://gotify.example.com',
        applicationToken: 'apptoken1',
        priority: 7,
        isEnabled: true,
        ...eventFlags,
      });
    });

    it('falls back to priority 5 when gotifyPriority is not numeric', () => {
      const model = baseModel({
        gotifyServerUrl: 'https://gotify.example.com',
        gotifyApplicationToken: 'apptoken1',
        gotifyPriority: 'not-a-number',
      });

      expect(descriptor.buildRequest(model).priority).toBe(5);
    });

    it('builds a test request, parsing the priority string', () => {
      const model = baseModel({
        gotifyServerUrl: 'https://gotify.example.com',
        gotifyApplicationToken: 'apptoken1',
        gotifyPriority: '3',
      });

      expect(descriptor.buildTestRequest(model, 'p1')).toEqual({
        serverUrl: 'https://gotify.example.com',
        applicationToken: 'apptoken1',
        priority: 3,
        providerId: 'p1',
      });
    });

    it('requires serverUrl and applicationToken', () => {
      expect(descriptor.requiredFields(baseModel())).toEqual(['gotifyServerUrl', 'gotifyApplicationToken']);
    });
  });
});
