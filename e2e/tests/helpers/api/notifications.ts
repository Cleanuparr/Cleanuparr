import { ApiClient } from './client';

export type NotificationProviderType =
  | 'notifiarr'
  | 'apprise'
  | 'ntfy'
  | 'telegram'
  | 'discord'
  | 'pushover'
  | 'gotify';

export class NotificationsApi {
  constructor(private readonly client: ApiClient) {}

  list(): Promise<Response> {
    return this.client.get('/api/configuration/notification_providers');
  }

  appriseCliStatus(): Promise<Response> {
    return this.client.get('/api/configuration/notification_providers/apprise/cli-status');
  }

  create(type: NotificationProviderType, body: Record<string, unknown>): Promise<Response> {
    return this.client.post(`/api/configuration/notification_providers/${type}`, body);
  }

  update(
    type: NotificationProviderType,
    id: string,
    body: Record<string, unknown>,
  ): Promise<Response> {
    return this.client.put(`/api/configuration/notification_providers/${type}/${id}`, body);
  }

  delete(id: string): Promise<Response> {
    return this.client.delete(`/api/configuration/notification_providers/${id}`);
  }

  test(type: NotificationProviderType, body: Record<string, unknown>): Promise<Response> {
    return this.client.post(`/api/configuration/notification_providers/${type}/test`, body);
  }
}
