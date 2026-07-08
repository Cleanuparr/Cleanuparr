import { ApiClient } from './client';

export class BlacklistSyncApi {
  constructor(private readonly client: ApiClient) {}

  getConfig(): Promise<Response> {
    return this.client.get('/api/configuration/blacklist_sync');
  }

  updateConfig(body: Record<string, unknown>): Promise<Response> {
    return this.client.put('/api/configuration/blacklist_sync', body);
  }
}
