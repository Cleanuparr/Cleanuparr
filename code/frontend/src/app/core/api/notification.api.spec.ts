import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { NotificationApi } from './notification.api';
import { NotificationProviderType } from '@shared/models/enums';
import { NOTIFICATION_PROVIDER_DESCRIPTORS } from '@shared/utils/notification-provider.descriptors';

type HttpPostStub = (url: string, body: unknown) => Observable<null>;
type HttpPutStub = (url: string, body: unknown) => Observable<null>;
type HttpGetStub = (url: string) => Observable<null>;
type HttpDeleteStub = (url: string) => Observable<void>;

describe('NotificationApi', () => {
  function setup() {
    const post = vi.fn<HttpPostStub>(() => of(null));
    const put = vi.fn<HttpPutStub>(() => of(null));
    const get = vi.fn<HttpGetStub>(() => of(null));
    const deleteMethod = vi.fn<HttpDeleteStub>(() => of(undefined));
    TestBed.configureTestingModule({
      providers: [{
        provide: HttpClient,
        useValue: { post, put, get, delete: deleteMethod },
      }],
    });
    return {
      api: TestBed.inject(NotificationApi),
      post,
      put,
      get,
      deleteMethod,
    };
  }

  describe('create', () => {
    it.each([
      [NotificationProviderType.Discord, 'discord'],
      [NotificationProviderType.Telegram, 'telegram'],
      [NotificationProviderType.Notifiarr, 'notifiarr'],
      [NotificationProviderType.Apprise, 'apprise'],
      [NotificationProviderType.Ntfy, 'ntfy'],
      [NotificationProviderType.Pushover, 'pushover'],
      [NotificationProviderType.Gotify, 'gotify'],
    ])('creates %s provider via POST to /api/configuration/notification_providers/%s', (type, segment) => {
      const { api, post } = setup();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[type] as any;
      const body = { name: 'test' };

      api.create(descriptor, body);

      expect(post).toHaveBeenCalledWith(
        `/api/configuration/notification_providers/${segment}`,
        body,
      );
    });
  });

  describe('update', () => {
    it.each([
      [NotificationProviderType.Discord, 'discord'],
      [NotificationProviderType.Telegram, 'telegram'],
      [NotificationProviderType.Notifiarr, 'notifiarr'],
      [NotificationProviderType.Apprise, 'apprise'],
      [NotificationProviderType.Ntfy, 'ntfy'],
      [NotificationProviderType.Pushover, 'pushover'],
      [NotificationProviderType.Gotify, 'gotify'],
    ])('updates %s provider via PUT to /api/configuration/notification_providers/%s/id-1', (type, segment) => {
      const { api, put } = setup();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[type] as any;
      const body = { name: 'updated' };

      api.update(descriptor, 'id-1', body);

      expect(put).toHaveBeenCalledWith(
        `/api/configuration/notification_providers/${segment}/id-1`,
        body,
      );
    });
  });

  describe('test', () => {
    it.each([
      [NotificationProviderType.Discord, 'discord'],
      [NotificationProviderType.Telegram, 'telegram'],
      [NotificationProviderType.Notifiarr, 'notifiarr'],
      [NotificationProviderType.Apprise, 'apprise'],
      [NotificationProviderType.Ntfy, 'ntfy'],
      [NotificationProviderType.Pushover, 'pushover'],
      [NotificationProviderType.Gotify, 'gotify'],
    ])('tests %s provider via POST to /api/configuration/notification_providers/%s/test', (type, segment) => {
      const { api, post } = setup();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const descriptor = NOTIFICATION_PROVIDER_DESCRIPTORS[type] as any;
      const body = { webhookUrl: 'test' };

      api.test(descriptor, body);

      expect(post).toHaveBeenCalledWith(
        `/api/configuration/notification_providers/${segment}/test`,
        body,
      );
    });
  });

  it('gets all providers via GET', () => {
    const { api, get } = setup();

    api.getProviders();

    expect(get).toHaveBeenCalledWith('/api/configuration/notification_providers');
  });

  it('gets Apprise CLI status via GET', () => {
    const { api, get } = setup();

    api.getAppriseCliStatus();

    expect(get).toHaveBeenCalledWith('/api/configuration/notification_providers/apprise/cli-status');
  });

  it('deletes provider via DELETE', () => {
    const { api, deleteMethod } = setup();

    api.deleteProvider('id-1');

    expect(deleteMethod).toHaveBeenCalledWith('/api/configuration/notification_providers/id-1');
  });
});
