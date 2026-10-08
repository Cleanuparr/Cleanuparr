import { ApiClient } from './client';

export type QueueRuleKind = 'stall' | 'slow';

export class QueueCleanerApi {
  constructor(private readonly client: ApiClient) {}

  getConfig(): Promise<Response> {
    return this.client.get('/api/configuration/queue_cleaner');
  }

  updateConfig(body: Record<string, unknown>): Promise<Response> {
    return this.client.put('/api/configuration/queue_cleaner', body);
  }

  async getJsonConfig(): Promise<Record<string, unknown>> {
    const res = await this.getConfig();
    if (!res.ok) {
      throw new Error(`GET queue cleaner config failed: ${res.status} ${await res.text()}`);
    }
    return res.json();
  }

  /** Merges overrides onto the current config. */
  async patch(overrides: Record<string, unknown>): Promise<void> {
    const current = await this.getJsonConfig();
    const merged = { ...current, ...overrides };
    const res = await this.updateConfig(merged);
    if (!res.ok) {
      throw new Error(`PUT queue cleaner config failed: ${res.status} ${await res.text()}`);
    }
  }

  listRules(kind: QueueRuleKind): Promise<Response> {
    return this.client.get(`/api/queue-rules/${kind}`);
  }

  createRule(kind: QueueRuleKind, body: Record<string, unknown>): Promise<Response> {
    return this.client.post(`/api/queue-rules/${kind}`, body);
  }

  updateRule(kind: QueueRuleKind, id: string, body: Record<string, unknown>): Promise<Response> {
    return this.client.put(`/api/queue-rules/${kind}/${id}`, body);
  }

  deleteRule(kind: QueueRuleKind, id: string): Promise<Response> {
    return this.client.delete(`/api/queue-rules/${kind}/${id}`);
  }
}
