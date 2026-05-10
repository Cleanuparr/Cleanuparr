import { ApiClient } from './client';

export class SeekerApi {
  constructor(private readonly client: ApiClient) {}

  getConfig(): Promise<Response> {
    return this.client.get('/api/configuration/seeker');
  }

  updateConfig(body: Record<string, unknown>): Promise<Response> {
    return this.client.put('/api/configuration/seeker', body);
  }

  listCustomFormatScores(query?: Record<string, string>): Promise<Response> {
    const qs = query ? '?' + new URLSearchParams(query).toString() : '';
    return this.client.get(`/api/seeker/cf-scores${qs}`);
  }

  listCustomFormatScoreUpgrades(): Promise<Response> {
    return this.client.get('/api/seeker/cf-scores/upgrades');
  }

  listCustomFormatScoreInstances(): Promise<Response> {
    return this.client.get('/api/seeker/cf-scores/instances');
  }

  getCustomFormatScoreStats(): Promise<Response> {
    return this.client.get('/api/seeker/cf-scores/stats');
  }

  getCustomFormatScoreHistory(instanceId: string, itemId: string): Promise<Response> {
    return this.client.get(`/api/seeker/cf-scores/${instanceId}/${itemId}/history`);
  }

  getSearchStatsSummary(): Promise<Response> {
    return this.client.get('/api/seeker/search-stats/summary');
  }

  getSearchEvents(query?: Record<string, string>): Promise<Response> {
    const qs = query ? '?' + new URLSearchParams(query).toString() : '';
    return this.client.get(`/api/seeker/search-stats/events${qs}`);
  }
}
