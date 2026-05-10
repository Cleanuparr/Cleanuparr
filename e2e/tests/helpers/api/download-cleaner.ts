import { ApiClient } from './client';

export class DownloadCleanerApi {
  constructor(private readonly client: ApiClient) {}

  getConfig(): Promise<Response> {
    return this.client.get('/api/configuration/download_cleaner');
  }

  updateConfig(body: Record<string, unknown>): Promise<Response> {
    return this.client.put('/api/configuration/download_cleaner', body);
  }

  listSeedingRules(downloadClientId: string): Promise<Response> {
    return this.client.get(`/api/seeding-rules/${downloadClientId}`);
  }

  createSeedingRule(downloadClientId: string, body: Record<string, unknown>): Promise<Response> {
    return this.client.post(`/api/seeding-rules/${downloadClientId}`, body);
  }

  updateSeedingRule(id: string, body: Record<string, unknown>): Promise<Response> {
    return this.client.put(`/api/seeding-rules/${id}`, body);
  }

  deleteSeedingRule(id: string): Promise<Response> {
    return this.client.delete(`/api/seeding-rules/${id}`);
  }

  reorderSeedingRules(downloadClientId: string, orderedIds: string[]): Promise<Response> {
    return this.client.put(`/api/seeding-rules/${downloadClientId}/reorder`, { orderedIds });
  }

  getUnlinkedConfig(downloadClientId: string): Promise<Response> {
    return this.client.get(`/api/unlinked-config/${downloadClientId}`);
  }

  updateUnlinkedConfig(downloadClientId: string, body: Record<string, unknown>): Promise<Response> {
    return this.client.put(`/api/unlinked-config/${downloadClientId}`, body);
  }
}
